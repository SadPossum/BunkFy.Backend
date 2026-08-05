namespace BunkFy.Modules.DataRights.Persistence.Repositories;

using System.Runtime.CompilerServices;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Runtime.Identity;
using Microsoft.EntityFrameworkCore;

internal sealed class DataRightsResponseDeadlineAlertRepository(
    DataRightsDbContext dbContext,
    IIdGenerator idGenerator)
    : IDataRightsResponseDeadlineAlertRepository
{
    public async Task<DataRightsResponseDeadlineAlertClaimResult> ClaimAsync(
        DateTimeOffset nowUtc,
        DateTimeOffset dueSoonUntilUtc,
        int batchSize,
        CancellationToken cancellationToken)
    {
        DataRightsCase[] candidates = await EligibleCases(dbContext.Cases)
            .Where(dataRightsCase =>
                dataRightsCase.DueAtUtc <= dueSoonUntilUtc &&
                (dataRightsCase.DueAtUtc <= nowUtc
                    ? !dbContext.ResponseDeadlineAlertDispatches.Any(dispatch =>
                        dispatch.CaseId == dataRightsCase.Id &&
                        dispatch.AlertKind ==
                            DataRightsResponseDeadlineAlertKind.Overdue)
                    : !dbContext.ResponseDeadlineAlertDispatches.Any(dispatch =>
                        dispatch.CaseId == dataRightsCase.Id &&
                        dispatch.AlertKind ==
                            DataRightsResponseDeadlineAlertKind.DueSoon)))
            .OrderBy(dataRightsCase => dataRightsCase.DueAtUtc)
            .ThenBy(dataRightsCase => dataRightsCase.Id)
            .Take(batchSize)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        List<DataRightsResponseDeadlineAlertDispatch> dispatches =
            new(candidates.Length);
        foreach (DataRightsCase candidate in candidates)
        {
            DataRightsResponseDeadlineAlertKind alertKind =
                candidate.DueAtUtc!.Value <= nowUtc
                    ? DataRightsResponseDeadlineAlertKind.Overdue
                    : DataRightsResponseDeadlineAlertKind.DueSoon;
            Guid dispatchId = idGenerator.NewId();
            DataRightsResponseDeadlineAlertDispatchReceipt receipt =
                DataRightsResponseDeadlineAlertDispatchReceipt.Create(
                    dispatchId,
                    candidate.ScopeId,
                    candidate.Id,
                    candidate.PropertyId!.Value,
                    alertKind,
                    candidate.DueAtUtc.Value,
                    nowUtc);
            dbContext.ResponseDeadlineAlertDispatches.Add(receipt);
            dispatches.Add(new(
                dispatchId,
                candidate.ScopeId,
                candidate.Id,
                candidate.PropertyId.Value,
                alertKind,
                candidate.DueAtUtc.Value));
        }

        return new(candidates.Length, dispatches);
    }

    public async Task<IReadOnlyList<string>> ListScheduleScopeIdsAsync(
        CancellationToken cancellationToken) => await this.ScheduleScopeIds()
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

    public async IAsyncEnumerable<string> StreamScheduleScopeIdsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (string scopeId in this.ScheduleScopeIds()
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            yield return scopeId;
        }
    }

    private IQueryable<string> ScheduleScopeIds() => EligibleCases(
        dbContext.Cases.IgnoreQueryFilters().AsNoTracking())
        .Select(dataRightsCase => dataRightsCase.ScopeId)
        .Distinct()
        .Order();

    private static IQueryable<DataRightsCase> EligibleCases(
        IQueryable<DataRightsCase> cases) => cases.Where(dataRightsCase =>
            dataRightsCase.Kind == DataRightsCaseKind.GuestRights &&
            dataRightsCase.PropertyId != null &&
            dataRightsCase.DueAtUtc != null &&
            dataRightsCase.ResponseDeadlinePolicyEvidence != null &&
            (dataRightsCase.RequesterRelationship ==
                DataRightsRequesterRelation.DataSubject ||
             dataRightsCase.RequesterRelationship ==
                DataRightsRequesterRelation.AuthorizedRepresentative) &&
            dataRightsCase.Status != DataRightsCaseState.Denied &&
            dataRightsCase.Status != DataRightsCaseState.Completed &&
            dataRightsCase.Status != DataRightsCaseState.PartiallyCompleted &&
            dataRightsCase.Status != DataRightsCaseState.Canceled);
}
