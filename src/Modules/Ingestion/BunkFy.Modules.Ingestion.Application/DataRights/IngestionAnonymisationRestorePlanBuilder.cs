namespace BunkFy.Modules.Ingestion.Application.DataRights;

using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.DataRights;
using BunkFy.Modules.Ingestion.Domain.Proposals;
using BunkFy.Modules.Ingestion.Domain.Receipts;
using BunkFy.Modules.Ingestion.Domain.Reprocessing;
using BunkFy.Modules.Ingestion.Domain.Reservations;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;

internal static class IngestionAnonymisationRestorePlanBuilder
{
    public static Result<
        IReadOnlyList<IngestionAnonymisationRecordPlanEntry>> Create(
            IIdGenerator ids,
            string tenantId,
            IngestionAnonymisationRestoreGraph graph,
            DateTimeOffset createdAtUtc)
    {
        List<IngestionAnonymisationRecordPlanEntry> plan = [];
        Result added = Add(
            ids,
            plan,
            tenantId,
            graph.SourceLink.Id,
            IngestionAnonymisationRecordKind.ReservationSourceLink,
            graph.SourceLink.Id,
            graph.SourceLink.Version,
            requiresMutation: true,
            rawPayloadFileId: null,
            rawPayloadConnectionId: null,
            createdAtUtc);
        if (added.IsFailure)
        {
            return Failure(added.Error);
        }

        foreach (ObservationReceipt receipt in graph.Receipts)
        {
            bool deleteRawPayload =
                receipt.RawPayloadRetentionState ==
                    RawPayloadRetentionState.Available;
            added = Add(
                ids,
                plan,
                tenantId,
                graph.SourceLink.Id,
                IngestionAnonymisationRecordKind.ObservationReceipt,
                receipt.Id,
                receipt.RawPayloadVersion,
                requiresMutation: true,
                deleteRawPayload ? receipt.RawPayloadFileId : null,
                deleteRawPayload ? receipt.ConnectionId : null,
                createdAtUtc);
            if (added.IsFailure)
            {
                return Failure(added.Error);
            }
        }

        foreach (ChangeProposal proposal in graph.Proposals)
        {
            added = Add(
                ids,
                plan,
                tenantId,
                graph.SourceLink.Id,
                IngestionAnonymisationRecordKind.ChangeProposal,
                proposal.Id,
                proposal.Version,
                requiresMutation: true,
                rawPayloadFileId: null,
                rawPayloadConnectionId: null,
                createdAtUtc);
            if (added.IsFailure)
            {
                return Failure(added.Error);
            }
        }

        foreach (ReservationDispatch dispatch in graph.Dispatches)
        {
            added = Add(
                ids,
                plan,
                tenantId,
                graph.SourceLink.Id,
                IngestionAnonymisationRecordKind.ReservationDispatch,
                dispatch.Id,
                dispatch.Version,
                requiresMutation: true,
                rawPayloadFileId: null,
                rawPayloadConnectionId: null,
                createdAtUtc);
            if (added.IsFailure)
            {
                return Failure(added.Error);
            }
        }

        foreach (ObservationReprocessingAttempt attempt in graph.Attempts)
        {
            added = Add(
                ids,
                plan,
                tenantId,
                graph.SourceLink.Id,
                IngestionAnonymisationRecordKind
                    .ObservationReprocessingAttempt,
                attempt.Id,
                attempt.Version,
                requiresMutation: false,
                rawPayloadFileId: null,
                rawPayloadConnectionId: null,
                createdAtUtc);
            if (added.IsFailure)
            {
                return Failure(added.Error);
            }
        }

        foreach (ObservationReprocessingOutput output in graph.Outputs)
        {
            added = Add(
                ids,
                plan,
                tenantId,
                graph.SourceLink.Id,
                IngestionAnonymisationRecordKind
                    .ObservationReprocessingOutput,
                output.Id,
                output.Version,
                requiresMutation: true,
                rawPayloadFileId: null,
                rawPayloadConnectionId: null,
                createdAtUtc);
            if (added.IsFailure)
            {
                return Failure(added.Error);
            }
        }

        return Result.Success<
            IReadOnlyList<IngestionAnonymisationRecordPlanEntry>>(plan);
    }

    private static Result Add(
        IIdGenerator ids,
        List<IngestionAnonymisationRecordPlanEntry> plan,
        string tenantId,
        Guid tombstoneId,
        IngestionAnonymisationRecordKind kind,
        Guid recordId,
        long selectedVersion,
        bool requiresMutation,
        Guid? rawPayloadFileId,
        Guid? rawPayloadConnectionId,
        DateTimeOffset createdAtUtc)
    {
        long reductionVersion = requiresMutation
            ? checked(selectedVersion + 1)
            : selectedVersion;
        long resultingVersion = rawPayloadFileId.HasValue
            ? checked(reductionVersion + 1)
            : reductionVersion;
        Result<IngestionAnonymisationRecordPlanEntry> created =
            IngestionAnonymisationRecordPlanEntry.Create(
                ids.NewId(),
                tenantId,
                tombstoneId,
                kind,
                recordId,
                selectedVersion,
                reductionVersion,
                resultingVersion,
                rawPayloadFileId,
                rawPayloadConnectionId,
                createdAtUtc);
        if (created.IsFailure)
        {
            return Result.Failure(created.Error);
        }

        plan.Add(created.Value);
        return Result.Success();
    }

    private static Result<
        IReadOnlyList<IngestionAnonymisationRecordPlanEntry>> Failure(
            Error error) =>
        Result.Failure<
            IReadOnlyList<IngestionAnonymisationRecordPlanEntry>>(error);
}
