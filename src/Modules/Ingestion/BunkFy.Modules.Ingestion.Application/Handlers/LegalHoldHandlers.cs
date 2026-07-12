namespace BunkFy.Modules.Ingestion.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Application.Queries;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Ingestion.Domain.LegalHolds;

internal sealed class PlaceLegalHoldCommandHandler(
    ILegalHoldRepository legalHolds,
    IRetentionFenceRepository retentionFence,
    IScopeContext scopeContext,
    ISystemClock clock,
    IIdGenerator ids)
    : ICommandHandler<PlaceLegalHoldCommand, LegalHoldDto>
{
    public async Task<Result<LegalHoldDto>> HandleAsync(
        PlaceLegalHoldCommand command,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled || string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<LegalHoldDto>(IngestionApplicationErrors.ScopeRequired);
        }

        if (!await retentionFence.TryAdvanceAsync(command.PropertyId, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result.Failure<LegalHoldDto>(IngestionApplicationErrors.PropertyNotFound);
        }

        if (await legalHolds.HasPurgingRawPayloadsAsync(command.PropertyId, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result.Failure<LegalHoldDto>(IngestionApplicationErrors.LegalHoldPurgeInProgress);
        }

        Result<LegalHold> placed = LegalHold.Place(
            ids.NewId(),
            scopeContext.ScopeId,
            command.PropertyId,
            command.Reason,
            command.PlacedBy,
            clock.UtcNow);
        if (placed.IsFailure)
        {
            return Result.Failure<LegalHoldDto>(placed.Error);
        }

        await legalHolds.AddAsync(placed.Value, cancellationToken).ConfigureAwait(false);
        return Result.Success(LegalHoldMappings.Map(placed.Value));
    }
}

internal sealed class ReleaseLegalHoldCommandHandler(
    ILegalHoldRepository legalHolds,
    IRetentionFenceRepository retentionFence,
    ISystemClock clock)
    : ICommandHandler<ReleaseLegalHoldCommand, LegalHoldDto>
{
    public async Task<Result<LegalHoldDto>> HandleAsync(
        ReleaseLegalHoldCommand command,
        CancellationToken cancellationToken)
    {
        LegalHold? legalHold = await legalHolds.GetAsync(
            command.PropertyId, command.HoldId, cancellationToken).ConfigureAwait(false);
        if (legalHold is null)
        {
            return Result.Failure<LegalHoldDto>(IngestionApplicationErrors.LegalHoldNotFound);
        }

        if (!await retentionFence.TryAdvanceAsync(command.PropertyId, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result.Failure<LegalHoldDto>(IngestionApplicationErrors.PropertyNotFound);
        }

        Result released = legalHold.Release(
            command.ExpectedVersion,
            command.ReleasedBy,
            command.ReleaseReason,
            clock.UtcNow);
        return released.IsSuccess
            ? Result.Success(LegalHoldMappings.Map(legalHold))
            : Result.Failure<LegalHoldDto>(released.Error);
    }
}

internal sealed class GetLegalHoldQueryHandler(ILegalHoldReader legalHolds)
    : IQueryHandler<GetLegalHoldQuery, LegalHoldDto>
{
    public async Task<Result<LegalHoldDto>> HandleAsync(
        GetLegalHoldQuery query,
        CancellationToken cancellationToken)
    {
        LegalHoldDto? legalHold = await legalHolds.GetAsync(
            query.PropertyId, query.HoldId, cancellationToken).ConfigureAwait(false);
        return legalHold is null
            ? Result.Failure<LegalHoldDto>(IngestionApplicationErrors.LegalHoldNotFound)
            : Result.Success(legalHold);
    }
}

internal sealed class ListLegalHoldsQueryHandler(ILegalHoldReader legalHolds)
    : IQueryHandler<ListLegalHoldsQuery, LegalHoldListResponse>
{
    public async Task<Result<LegalHoldListResponse>> HandleAsync(
        ListLegalHoldsQuery query,
        CancellationToken cancellationToken)
    {
        if (query.Status.HasValue &&
            (query.Status.Value == LegalHoldStatus.Unknown || !Enum.IsDefined(query.Status.Value)))
        {
            return Result.Failure<LegalHoldListResponse>(IngestionApplicationErrors.LegalHoldStatusInvalid);
        }

        return Result.Success(await legalHolds.ListAsync(
            query.PropertyId,
            query.Status,
            PageRequest.Normalize(query.Page, query.PageSize),
            cancellationToken).ConfigureAwait(false));
    }
}

internal static class LegalHoldMappings
{
    public static LegalHoldDto Map(LegalHold legalHold) => new(
        legalHold.Id,
        legalHold.PropertyId,
        legalHold.Reason,
        (LegalHoldStatus)(int)legalHold.State,
        legalHold.PlacedBy,
        legalHold.PlacedAtUtc,
        legalHold.ReleasedBy,
        legalHold.ReleaseReason,
        legalHold.ReleasedAtUtc,
        legalHold.Version);
}
