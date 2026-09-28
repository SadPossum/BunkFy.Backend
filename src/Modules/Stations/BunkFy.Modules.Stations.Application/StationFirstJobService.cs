namespace BunkFy.Modules.Stations.Application;

using BunkFy.Modules.Inventory.Contracts.Stations;
using BunkFy.Modules.Reservations.Contracts.Stations;
using BunkFy.Modules.Stations.Contracts;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Microsoft.Extensions.DependencyInjection;

public sealed record StationFirstJobArrival(StationDueArrival Reservation, IReadOnlyList<StationInventoryLabel> Places);
public sealed record StationFirstJobPage(StationReservationState State, IReadOnlyList<StationFirstJobArrival> Items,
    StationArrivalCursor? Continuation = null, Guid? PropertyId = null, DateOnly? PropertyLocalDate = null);

/// <summary>One unhosted station job; no primary principal, client scope/date, general directory, or persistent authority cache.</summary>
public sealed class StationFirstJobService(IServiceScopeFactory scopes, IStationCredentialBootstrap bootstrap, ISystemClock clock)
{
    public Task<StationFirstJobPage> ListAsync(string opaqueCredential, StationActorCoordinate actor, int pageSize = 25,
        StationArrivalCursor? after = null, CancellationToken cancellationToken = default) =>
        this.WithActorAsync(opaqueCredential, actor, async context =>
        {
            if (pageSize is < 1 or > 25)
            { return new(StationReservationState.Conflict, []); }
            var page = await context.Services.GetRequiredService<IStationReservationOperations>()
                .ListAsync(context.Device.PropertyId, context.LocalDate, pageSize, after, cancellationToken).ConfigureAwait(false);
            var state = await this.RevalidateAsync(context, opaqueCredential, actor, cancellationToken).ConfigureAwait(false);
            if (state != StationReservationState.Ready)
            { return new(state, []); }
            if (page.State != StationReservationState.Ready)
            { return new(page.State, []); }
            var labels = await this.LabelsAsync(context, opaqueCredential, actor, page.Items, cancellationToken).ConfigureAwait(false);
            if (labels.State != StationReservationState.Ready)
            { return new(labels.State, []); }
            state = await this.RevalidateAsync(context, opaqueCredential, actor, cancellationToken).ConfigureAwait(false);
            return state == StationReservationState.Ready
                ? new(StationReservationState.Ready, page.Items.Select(x => new StationFirstJobArrival(x,
                    x.Units.Select(u => labels.Items.Single(l => l.InventoryUnitId == u.InventoryUnitId)).ToArray())).ToArray(),
                    page.Continuation, context.Device.PropertyId, context.LocalDate)
                : new(state, []);
        }, state => new StationFirstJobPage(state, []), cancellationToken);

    public Task<StationCheckInResult> CheckInAsync(string opaqueCredential, StationActorCoordinate actor,
        Guid operationId, Guid reservationId, long expectedVersion, CancellationToken cancellationToken = default) =>
        this.WithActorAsync(opaqueCredential, actor, async context =>
        {
            var operations = context.Services.GetRequiredService<IStationReservationOperations>();
            var provenance = Provenance(context.Device, actor);
            var preparation = await operations.PrepareAsync(context.Device.PropertyId, reservationId, operationId,
                expectedVersion, context.LocalDate, provenance, cancellationToken).ConfigureAwait(false);
            var state = await this.RevalidateAsync(context, opaqueCredential, actor, cancellationToken).ConfigureAwait(false);
            if (state != StationReservationState.Ready)
            { return new(state); }
            if (preparation.State != StationReservationState.Ready)
            { return new(preparation.State); }
            if (!preparation.Replay)
            {
                if (preparation.Arrival is null)
                { return new(StationReservationState.Incomplete); }
                var labels = await this.LabelsAsync(context, opaqueCredential, actor, [preparation.Arrival], cancellationToken).ConfigureAwait(false);
                if (labels.State != StationReservationState.Ready)
                { return new(labels.State); }
            }
            state = await this.RevalidateAsync(context, opaqueCredential, actor, cancellationToken).ConfigureAwait(false);
            if (state != StationReservationState.Ready)
            { return new(state); }
            var result = await operations.CheckInAsync(context.Device.PropertyId, reservationId, operationId, expectedVersion,
                preparation, provenance, cancellationToken).ConfigureAwait(false);
            state = await this.RevalidateAsync(context, opaqueCredential, actor, cancellationToken).ConfigureAwait(false);
            return state == StationReservationState.Ready ? result : new(state);
        }, state => new StationCheckInResult(state), cancellationToken);

    private async Task<T> WithActorAsync<T>(string credential, StationActorCoordinate actor, Func<Context, Task<T>> action,
        Func<StationReservationState, T> failure, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (StationCredentialEncoding.Digest(credential) is null || actor.StaffMemberId == Guid.Empty ||
            actor.ActorSessionId == Guid.Empty || actor.Generation < 1 ||
            actor.AuthorityKind is not (StationAuthorityKind.LinkedStation or StationAuthorityKind.StationOnly))
        { return failure(StationReservationState.Denied); }
        try
        {
            var device = await bootstrap.FindAsync(credential, ct).ConfigureAwait(false);
            if (device is null || !Guid.TryParseExact(device.ScopeId, "D", out var tenant) || tenant == Guid.Empty ||
                tenant.ToString("D") != device.ScopeId || device.StationId == Guid.Empty || device.BrowserSessionId == Guid.Empty || device.PropertyId == Guid.Empty)
            { return failure(StationReservationState.Denied); }
            await using var child = scopes.CreateAsyncScope();
            var services = child.ServiceProvider;
            services.GetRequiredService<IScopeContextAccessor>().SetScope(device.ScopeId);
            var store = services.GetRequiredService<IStationRuntimeStore>();
            var facts = await store.ReadAsync(device, credential, null, clock.UtcNow, ct).ConfigureAwait(false);
            if (facts is null || !Current(device, facts, actor))
            { return failure(StationReservationState.Denied); }
            var admission = await services.GetRequiredService<StationAdmissionCoordinator>()
                .ObserveAsync(device.PropertyId, actor.StaffMemberId, facts.Credential!.Enrollment, ct).ConfigureAwait(false);
            if (admission.State != StationAdmissionState.Current || admission.PropertyLocalDate is not { } date)
            { return failure(FromAdmission(admission.State)); }
            var context = new Context(services, device, store, facts, date);
            var state = await this.RevalidateAsync(context, credential, actor, ct).ConfigureAwait(false);
            return state == StationReservationState.Ready ? await action(context).ConfigureAwait(false) : failure(state);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception) { return failure(StationReservationState.Unavailable); }
    }

    private async Task<StationReservationState> RevalidateAsync(Context context, string credential, StationActorCoordinate actor, CancellationToken ct)
    {
        var before = await context.Store.ReadAsync(context.Device, credential, null, clock.UtcNow, ct).ConfigureAwait(false);
        if (before is null || !Current(context.Device, before, actor) || context.Facts != before)
        { return StationReservationState.Conflict; }
        var admission = await context.Services.GetRequiredService<StationAdmissionCoordinator>()
            .ObserveAsync(context.Device.PropertyId, actor.StaffMemberId, before.Credential!.Enrollment, ct).ConfigureAwait(false);
        if (admission.State != StationAdmissionState.Current)
        { return FromAdmission(admission.State); }
        var after = await context.Store.ReadAsync(context.Device, credential, null, clock.UtcNow, ct).ConfigureAwait(false);
        return before == after && admission.PropertyLocalDate == context.LocalDate
            ? StationReservationState.Ready : StationReservationState.Conflict;
    }

    private async Task<Labels> LabelsAsync(Context context, string credential, StationActorCoordinate actor,
        IReadOnlyList<StationDueArrival> arrivals, CancellationToken ct)
    {
        if (arrivals.Count > 25 || arrivals.Any(x => x.Units.Count is < 1 or > 100))
        { return new(StationReservationState.Incomplete, []); }
        var expected = arrivals.SelectMany(x => x.Units).GroupBy(x => x.InventoryUnitId).OrderBy(x => x.Key).ToArray();
        if (expected.Length > 100 || expected.Any(g => g.Key == Guid.Empty || g.Distinct().Count() != 1))
        { return new(StationReservationState.Incomplete, []); }
        var reader = context.Services.GetRequiredService<IStationInventoryLabelReader>();
        List<StationInventoryLabel> result = [];
        foreach (var batch in expected.Chunk(25))
        {
            var state = await this.RevalidateAsync(context, credential, actor, ct).ConfigureAwait(false);
            if (state != StationReservationState.Ready)
            { return new(state, []); }
            var labels = await reader.ReadAsync(context.Device.PropertyId, batch.Select(x => x.Key).ToArray(), ct).ConfigureAwait(false);
            if (labels.State != StationInventoryLabelsState.Current)
            { return new(labels.State == StationInventoryLabelsState.Unavailable ? StationReservationState.Unavailable : StationReservationState.Incomplete, []); }
            if (labels.Items.Count != batch.Length || labels.Items.Select(x => x.InventoryUnitId).Distinct().Count() != batch.Length)
            { return new(StationReservationState.Incomplete, []); }
            foreach (var group in batch)
            {
                var unit = group.First();
                var label = labels.Items.SingleOrDefault(x => x.InventoryUnitId == unit.InventoryUnitId);
                if (label is null || label.PropertyId != context.Device.PropertyId || label.RoomId != unit.RoomId || label.BedId != unit.BedId ||
                    (int)label.Kind != unit.Kind || label.UnitVersion != unit.UnitVersion || label.ConfigurationVersion != unit.ConfigurationVersion ||
                    label.UnitSourceVersion < 1 || label.RoomSourceVersion < 1 || string.IsNullOrWhiteSpace(label.RoomName) ||
                    (unit.BedId is not null && (label.BedSourceVersion is null or < 1 || string.IsNullOrWhiteSpace(label.BedLabel))))
                { return new(StationReservationState.Incomplete, []); }
                result.Add(label);
            }
        }
        return new(StationReservationState.Ready, result);
    }

    private static bool Current(StationDeviceReference device, StationRuntimeFacts facts, StationActorCoordinate actor) =>
        facts.ActorCurrent && facts.Registered && facts.Session.Actor == actor && facts.Session.Generation == actor.Generation &&
        facts.Session.PropertyId == device.PropertyId && facts.Session.StationId == device.StationId && facts.Session.BrowserSessionId == device.BrowserSessionId &&
        facts.Credential is { Revoked: false, Enrollment.IsBound: true } credential && credential.StaffMemberId == actor.StaffMemberId &&
        (actor.AuthorityKind != StationAuthorityKind.StationOnly || (!facts.GrantRevoked && facts.GrantRevision is > 0));
    private static StationCheckInProvenance Provenance(StationDeviceReference device, StationActorCoordinate actor) =>
        new(device.StationId, device.BrowserSessionId, actor.StaffMemberId, actor.ActorSessionId, actor.Generation,
            actor.AuthorityKind switch
            {
                StationAuthorityKind.LinkedStation => StationReservationAuthority.LinkedStation,
                StationAuthorityKind.StationOnly => StationReservationAuthority.StationOnly,
                _ => StationReservationAuthority.Unknown
            });
    private static StationReservationState FromAdmission(StationAdmissionState state) => state switch
    {
        StationAdmissionState.Denied => StationReservationState.Denied,
        StationAdmissionState.StateChanged => StationReservationState.Conflict,
        _ => StationReservationState.Unavailable
    };
    private sealed record Context(IServiceProvider Services, StationDeviceReference Device, IStationRuntimeStore Store,
        StationRuntimeFacts Facts, DateOnly LocalDate);
    private sealed record Labels(StationReservationState State, IReadOnlyList<StationInventoryLabel> Items);
}
