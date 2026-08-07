namespace BunkFy.Modules.Staff.Application.Handlers;

using BunkFy.Modules.Staff.Application.Mapping;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;

internal sealed class StaffPropertyAssignmentChangeCoordinator(
    IStaffMemberMutationOperationRepository operations,
    ISystemClock clock,
    IIdGenerator ids)
{
    public Task<Result<StaffMemberMutationReceiptDto>> AssignAsync(
        StaffMember member,
        Guid operationId,
        Guid propertyId,
        bool isPrimary,
        DateOnly effectiveFrom,
        long expectedVersion,
        StaffPropertyAssignmentChangeValues values,
        CancellationToken cancellationToken) => this.ExecuteAsync(
        member,
        operationId,
        expectedVersion,
        StaffMemberMutationKind.AssignProperty,
        StaffPropertyAssignmentChangeFingerprint.ComputeAssignment(
            member.Id,
            propertyId,
            expectedVersion,
            values.PropertyJobTitle,
            isPrimary,
            effectiveFrom),
        (eventId, nowUtc) => member.AssignProperty(
            ids.NewId(),
            propertyId,
            values.PropertyJobTitle,
            isPrimary,
            effectiveFrom,
            expectedVersion,
            values.Actor.Value,
            eventId,
            nowUtc),
        cancellationToken);

    public Task<Result<StaffMemberMutationReceiptDto>> UnassignAsync(
        StaffMember member,
        Guid operationId,
        Guid propertyId,
        DateOnly effectiveTo,
        long expectedVersion,
        StaffPropertyUnassignmentChangeValues values,
        CancellationToken cancellationToken) => this.ExecuteAsync(
        member,
        operationId,
        expectedVersion,
        StaffMemberMutationKind.UnassignProperty,
        StaffPropertyAssignmentChangeFingerprint.ComputeUnassignment(
            member.Id,
            propertyId,
            expectedVersion,
            effectiveTo,
            values.Reason),
        (eventId, nowUtc) => member.UnassignProperty(
            propertyId,
            effectiveTo,
            expectedVersion,
            values.Actor.Value,
            values.Reason.Value,
            eventId,
            nowUtc),
        cancellationToken);

    private async Task<Result<StaffMemberMutationReceiptDto>> ExecuteAsync(
        StaffMember member,
        Guid operationId,
        long expectedVersion,
        StaffMemberMutationKind kind,
        string fingerprint,
        Func<Guid, DateTimeOffset, Result> mutate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(mutate);

        StaffMemberMutationOperationRecord? existing = await operations.GetAsync(
                member.Id,
                operationId,
                cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return existing.Matches(
                kind,
                member.Id,
                expectedVersion,
                fingerprint)
                ? Result.Success(existing.ToReceipt())
                : Result.Failure<StaffMemberMutationReceiptDto>(
                    StaffApplicationErrors.AssignmentOperationConflict);
        }

        DateTimeOffset nowUtc = StaffMutationTime.Normalize(clock.UtcNow);
        Result changed = mutate(ids.NewId(), nowUtc);
        if (changed.IsFailure)
        {
            return Result.Failure<StaffMemberMutationReceiptDto>(changed.Error);
        }

        StaffMemberMutationReceiptDto receipt = new(
            member.Id,
            StaffMappings.MapStatus(member.Status),
            member.Version,
            nowUtc);
        await operations.AddAsync(
            new StaffMemberMutationOperationRecord(
                operationId,
                member.ScopeId,
                member.Id,
                kind,
                expectedVersion,
                fingerprint,
                receipt.Status,
                receipt.Version,
                receipt.CompletedAtUtc),
            cancellationToken).ConfigureAwait(false);
        return Result.Success(receipt);
    }
}
