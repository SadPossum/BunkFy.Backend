namespace BunkFy.Modules.Ingestion.Persistence.Repositories;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Ingestion.Domain.Connections;
using BunkFy.Modules.Ingestion.Domain.Controls;
using BunkFy.Modules.Ingestion.Domain.Credentials;
using BunkFy.Modules.Ingestion.Domain.Receipts;
using BunkFy.Modules.Ingestion.Domain.Reprocessing;
using BunkFy.Modules.Ingestion.Domain.Runs;
using Microsoft.EntityFrameworkCore;

internal sealed partial class IngestionTenantTerminationContributor
{
    private async Task<long> ExportRecordsAsync(
        string tenantId,
        IDataRightsExportSink sink,
        CancellationToken cancellationToken)
    {
        long count = 0;
        count = await this.ExportConnectionsAsync(
            tenantId, sink, count, cancellationToken).ConfigureAwait(false);
        count = await this.ExportConnectionManagementOperationsAsync(
            tenantId, sink, count, cancellationToken).ConfigureAwait(false);
        count = await this.ExportCredentialsAsync(
            tenantId, sink, count, cancellationToken).ConfigureAwait(false);
        count = await this.ExportIngressControlsAsync(
            tenantId, sink, count, cancellationToken).ConfigureAwait(false);
        count = await this.ExportRunsAsync(
            tenantId, sink, count, cancellationToken).ConfigureAwait(false);
        count = await this.ExportReceiptsAsync(
            tenantId, sink, count, cancellationToken).ConfigureAwait(false);
        count = await this.ExportReprocessingAttemptsAsync(
            tenantId, sink, count, cancellationToken).ConfigureAwait(false);
        count = await this.ExportReprocessingOutputsAsync(
            tenantId, sink, count, cancellationToken).ConfigureAwait(false);
        count = await this.ExportProposalsAsync(
            tenantId, sink, count, cancellationToken).ConfigureAwait(false);
        count = await this.ExportSourceLinksAsync(
            tenantId, sink, count, cancellationToken).ConfigureAwait(false);
        count = await this.ExportDispatchesAsync(
            tenantId, sink, count, cancellationToken).ConfigureAwait(false);
        count = await this.ExportLegalHoldsAsync(
            tenantId, sink, count, cancellationToken).ConfigureAwait(false);
        count = await this.ExportRetentionExecutionsAsync(
            tenantId, sink, count, cancellationToken).ConfigureAwait(false);
        count = await this.ExportAnonymisationReceiptsAsync(
            tenantId, sink, count, cancellationToken).ConfigureAwait(false);
        count = await this.ExportAnonymisationTombstonesAsync(
            tenantId, sink, count, cancellationToken).ConfigureAwait(false);
        return await this.ExportLargeTextChunksAsync(
            tenantId, sink, count, cancellationToken).ConfigureAwait(false);
    }

    private async Task<long> ExportConnectionsAsync(
        string tenantId,
        IDataRightsExportSink sink,
        long count,
        CancellationToken cancellationToken)
    {
        await foreach (AdapterConnection connection in
            dbContext.AdapterConnections
                .AsNoTracking()
                .Where(item => item.ScopeId == tenantId)
                .OrderBy(item => item.Id)
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
        {
            IngestionAdapterConnectionTenantExport record = new(
                connection.ScopeId,
                connection.PropertyId,
                connection.Id,
                new IngestionAdapterConnectionStateTenantExport(
                    connection.AdapterType,
                    connection.ExecutionMode,
                    connection.ConflictPolicy,
                    connection.ConfigurationReference,
                    connection.Checkpoint,
                    connection.PollingIntervalSeconds,
                    connection.PollingScheduleMaxAttempts,
                    connection.PollingScheduleConfiguredAtUtc,
                    connection.RemoteLeaseRunId,
                    connection.RemoteLeaseId,
                    connection.RemoteLeaseClaimId,
                    connection.RemoteLeaseCredentialId,
                    connection.RemoteLeaseWorkerId,
                    connection.RemoteLeaseEpoch,
                    connection.RemoteLeaseExpiresAtUtc,
                    connection.State,
                    connection.Version,
                    connection.CreatedAtUtc,
                    connection.UpdatedAtUtc));
            await WriteAsync(
                IngestionTenantTerminationMetadata
                    .AdapterConnectionRecordType,
                connection.Id,
                connection.Version,
                record,
                sink,
                cancellationToken).ConfigureAwait(false);
            count = checked(count + 1);
        }

        return count;
    }

    private async Task<long> ExportCredentialsAsync(
        string tenantId,
        IDataRightsExportSink sink,
        long count,
        CancellationToken cancellationToken)
    {
        await foreach (AdapterIngressCredential credential in
            dbContext.AdapterIngressCredentials
                .AsNoTracking()
                .Where(item => item.ScopeId == tenantId)
                .OrderBy(item => item.Id)
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
        {
            IngestionAdapterCredentialTenantExport record = new(
                credential.ScopeId,
                credential.ConnectionId,
                credential.Id,
                new IngestionAdapterCredentialMetadataTenantExport(
                    credential.AdapterType,
                    credential.AdapterProtocolVersion,
                    credential.ConfigurationSchemaVersion,
                    credential.SourceSystem,
                    credential.Slot,
                    credential.Label,
                    credential.State,
                    credential.ExpiresAtUtc,
                    credential.CreatedBy,
                    credential.CreatedAtUtc,
                    credential.RevokedBy,
                    credential.RevokedAtUtc,
                    credential.LastAuthenticatedAtUtc,
                    credential.Version));
            long recordVersion = Math.Max(
                credential.Version,
                credential.LastAuthenticatedAtUtc?.UtcTicks ?? 1);
            await WriteAsync(
                IngestionTenantTerminationMetadata
                    .AdapterCredentialRecordType,
                credential.Id,
                recordVersion,
                record,
                sink,
                cancellationToken).ConfigureAwait(false);
            count = checked(count + 1);
        }

        return count;
    }

    private async Task<long> ExportConnectionManagementOperationsAsync(
        string tenantId,
        IDataRightsExportSink sink,
        long count,
        CancellationToken cancellationToken)
    {
        await foreach (IngestionConnectionManagementOperation operation in
            dbContext.ConnectionManagementOperations
                .AsNoTracking()
                .Where(item => item.ScopeId == tenantId)
                .OrderBy(item => item.ConnectionId)
                .ThenBy(item => item.Id)
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
        {
            IngestionConnectionManagementOperationTenantExport record = new(
                operation.ScopeId,
                operation.PropertyId,
                operation.ConnectionId,
                operation.Id,
                new IngestionConnectionManagementOperationStateTenantExport(
                    (int)operation.Kind,
                    operation.ExpectedVersion,
                    operation.ResultVersion,
                    operation.CompletedAtUtc));
            await WriteAsync(
                IngestionTenantTerminationMetadata
                    .ConnectionManagementOperationRecordType,
                operation.Id,
                1,
                record,
                sink,
                cancellationToken).ConfigureAwait(false);
            count = checked(count + 1);
        }

        return count;
    }

    private async Task<long> ExportIngressControlsAsync(
        string tenantId,
        IDataRightsExportSink sink,
        long count,
        CancellationToken cancellationToken)
    {
        await foreach (AdapterIngressTenantControl control in
            dbContext.AdapterIngressTenantControls
                .AsNoTracking()
                .Where(item => item.ScopeId == tenantId)
                .OrderBy(item => item.Id)
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
        {
            IngestionAdapterIngressControlTenantExport record = new(
                control.ScopeId,
                new IngestionAdapterIngressControlStateTenantExport(
                    control.IsSuspended,
                    control.LastReasonCode,
                    control.LastChangedBy,
                    control.LastChangedAtUtc,
                    control.SuspendedAtUtc,
                    control.ResumedAtUtc,
                    control.Version));
            await WriteAsync(
                IngestionTenantTerminationMetadata
                    .AdapterIngressControlRecordType,
                DataRightsExportRecordIds.CreateDeterministicChild(
                    IngressControlRecordNamespaceId,
                    control.Id),
                control.Version,
                record,
                sink,
                cancellationToken).ConfigureAwait(false);
            count = checked(count + 1);
        }

        return count;
    }

    private async Task<long> ExportRunsAsync(
        string tenantId,
        IDataRightsExportSink sink,
        long count,
        CancellationToken cancellationToken)
    {
        await foreach (IngestionRun run in dbContext.Runs
            .AsNoTracking()
            .Where(item => item.ScopeId == tenantId)
            .OrderBy(item => item.Id)
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            IngestionRunTenantExport record = new(
                run.ScopeId,
                run.PropertyId,
                run.ConnectionId,
                run.Id,
                new IngestionRunStateTenantExport(
                    run.ExecutionKind,
                    run.TaskRunId,
                    run.TaskAttempt,
                    run.RemoteLeaseId,
                    run.RemoteClaimId,
                    run.RemoteLeaseEpoch,
                    run.RemoteCredentialId,
                    run.RemoteWorkerId,
                    run.RemoteLeaseExpiresAtUtc,
                    run.StartingCheckpoint,
                    run.AcceptedCheckpoint,
                    run.State,
                    run.ObservedCount,
                    run.AcceptedCount,
                    run.RejectedCount,
                    run.ErrorCode,
                    run.Version,
                    run.StartedAtUtc,
                    run.CompletedAtUtc));
            await WriteAsync(
                IngestionTenantTerminationMetadata.IngestionRunRecordType,
                run.Id,
                run.Version,
                record,
                sink,
                cancellationToken).ConfigureAwait(false);
            count = checked(count + 1);
        }

        return count;
    }

    private async Task<long> ExportReceiptsAsync(
        string tenantId,
        IDataRightsExportSink sink,
        long count,
        CancellationToken cancellationToken)
    {
        await foreach (ObservationReceipt receipt in
            dbContext.ObservationReceipts
                .AsNoTracking()
                .Where(item => item.ScopeId == tenantId)
                .OrderBy(item => item.Id)
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
        {
            IngestionObservationReceiptTenantExport record = new(
                receipt.ScopeId,
                receipt.PropertyId,
                receipt.ConnectionId,
                receipt.Id,
                new IngestionObservationEvidenceTenantExport(
                    receipt.RunId,
                    receipt.OperationId,
                    receipt.SourceRecordType,
                    receipt.ExternalId,
                    receipt.SourceRevision,
                    receipt.DeduplicationKey,
                    receipt.ContentHash,
                    ToExport(receipt.AdapterProvenance),
                    ToExport(receipt.CountryPolicyEvidence),
                    receipt.RawPayloadFileId,
                    receipt.RawPayloadRetentionState,
                    receipt.RawPayloadRetainUntilUtc,
                    receipt.RawPayloadPurgeClaimId,
                    receipt.RawPayloadPurgeStartedAtUtc,
                    receipt.RawPayloadPurgedAtUtc,
                    receipt.RawPayloadVersion,
                    receipt.ActiveReprocessingAttemptId,
                    receipt.ReprocessingReservationExpiresAtUtc,
                    receipt.SourceReceiptId,
                    receipt.ReprocessingAttemptId,
                    receipt.ParserType,
                    receipt.ParserVersion,
                    receipt.ParserOutputIndex,
                    receipt.SourceUpdatedAtUtc,
                    receipt.ObservedAtUtc,
                    receipt.State,
                    receipt.RejectionReason,
                    receipt.ReceivedAtUtc,
                    receipt.ProcessedAtUtc,
                    receipt.AnonymisedAtUtc));
            await WriteAsync(
                IngestionTenantTerminationMetadata
                    .ObservationReceiptRecordType,
                receipt.Id,
                receipt.RawPayloadVersion,
                record,
                sink,
                cancellationToken).ConfigureAwait(false);
            count = checked(count + 1);
        }

        return count;
    }

    private async Task<long> ExportReprocessingAttemptsAsync(
        string tenantId,
        IDataRightsExportSink sink,
        long count,
        CancellationToken cancellationToken)
    {
        await foreach (ObservationReprocessingAttempt attempt in
            dbContext.ObservationReprocessingAttempts
                .AsNoTracking()
                .Where(item => item.ScopeId == tenantId)
                .OrderBy(item => item.Id)
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
        {
            IngestionReprocessingAttemptTenantExport record = new(
                attempt.ScopeId,
                attempt.PropertyId,
                attempt.ConnectionId,
                attempt.Id,
                new IngestionReprocessingAttemptStateTenantExport(
                    attempt.SourceReceiptId,
                    attempt.TaskRunId,
                    attempt.ParserType,
                    attempt.ParserVersion,
                    attempt.RequestedBy,
                    attempt.State,
                    attempt.LastTaskAttempt,
                    attempt.ParsedCount,
                    attempt.AcceptedCount,
                    attempt.DuplicateCount,
                    attempt.RejectedCount,
                    attempt.LastErrorCode,
                    attempt.RequestedAtUtc,
                    attempt.StartedAtUtc,
                    attempt.CompletedAtUtc,
                    attempt.ReservationExpiresAtUtc,
                    attempt.Version));
            await WriteAsync(
                IngestionTenantTerminationMetadata
                    .ObservationReprocessingAttemptRecordType,
                attempt.Id,
                attempt.Version,
                record,
                sink,
                cancellationToken).ConfigureAwait(false);
            count = checked(count + 1);
        }

        return count;
    }

    private async Task<long> ExportReprocessingOutputsAsync(
        string tenantId,
        IDataRightsExportSink sink,
        long count,
        CancellationToken cancellationToken)
    {
        await foreach (ObservationReprocessingOutput output in
            dbContext.ObservationReprocessingOutputs
                .AsNoTracking()
                .Where(item => item.ScopeId == tenantId)
                .OrderBy(item => item.Id)
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
        {
            IngestionReprocessingOutputTenantExport record = new(
                output.ScopeId,
                output.Id,
                new IngestionReprocessingOutputStateTenantExport(
                    output.AttemptId,
                    output.OutputIndex,
                    output.OperationId,
                    output.ReceiptId,
                    output.Disposition,
                    output.RecordType,
                    output.ExternalId,
                    output.SourceRevision,
                    output.ContentHash,
                    output.ErrorCode,
                    output.RecordedAtUtc,
                    output.Version,
                    output.AnonymisedAtUtc));
            await WriteAsync(
                IngestionTenantTerminationMetadata
                    .ObservationReprocessingOutputRecordType,
                output.Id,
                output.Version,
                record,
                sink,
                cancellationToken).ConfigureAwait(false);
            count = checked(count + 1);
        }

        return count;
    }

    private static IngestionAdapterProvenanceTenantExport? ToExport(
        ObservationAdapterProvenance? value) =>
        value is null
            ? null
            : new(
                value.CredentialId,
                value.AdapterType,
                value.AdapterProtocolVersion,
                value.ConfigurationSchemaVersion,
                value.SourceSystem,
                value.CustomerOwner);

    private static IngestionCountryPolicyEvidenceTenantExport? ToExport(
        ObservationCountryPolicyEvidence? value) =>
        value is null
            ? null
            : new(
                value.OperatingCountryCode,
                value.PolicyId,
                value.PolicyVersion,
                value.DataRegionId,
                value.TransferProfileId,
                value.RetentionPolicyId,
                value.RetentionPolicyVersion,
                value.ContentSha256,
                value.PurposeCode,
                value.ProcessingSurface,
                value.SourceProvenance,
                value.PolicyEffectiveAtUtc,
                value.PolicyExpiresAtUtc,
                value.EvaluatedAtUtc);
}
