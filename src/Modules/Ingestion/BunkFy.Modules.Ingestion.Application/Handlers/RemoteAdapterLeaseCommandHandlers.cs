namespace BunkFy.Modules.Ingestion.Application.Handlers;

using BunkFy.DataGovernance;
using BunkFy.Adapter.Abstractions;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using BunkFy.Modules.Ingestion.Application.Adapters;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Application.Policies;
using BunkFy.Modules.Ingestion.Domain.Connections;
using BunkFy.Modules.Ingestion.Domain.Runs;

internal sealed class ClaimRemoteAdapterLeaseCommandHandler(
    IAdapterConnectionRepository connections,
    IIngestionCountryPolicyAdmission countryPolicy,
    IIngestionRunRepository runs,
    IAdapterDescriptorRegistry descriptors,
    IScopeContext scopeContext,
    ISystemClock clock,
    IIdGenerator ids)
    : ICommandHandler<ClaimRemoteAdapterLeaseCommand, AdapterRemoteLeaseClaimResponse>
{
    public async Task<Result<AdapterRemoteLeaseClaimResponse>> HandleAsync(
        ClaimRemoteAdapterLeaseCommand command,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled || string.IsNullOrWhiteSpace(scopeContext.ScopeId) ||
            command.Request is null || command.Request.ClaimId == Guid.Empty || command.Request.WorkerId == Guid.Empty ||
            command.Request.RequestedLeaseSeconds is < AdapterRemoteLeaseContractLimits.MinimumLeaseSeconds or
                > AdapterRemoteLeaseContractLimits.MaximumLeaseSeconds)
        {
            return Result.Failure<AdapterRemoteLeaseClaimResponse>(
                IngestionApplicationErrors.RemoteLeaseClaimInvalid);
        }

        AdapterConnection? connection = await connections.GetAsync(command.ConnectionId, cancellationToken)
            .ConfigureAwait(false);
        if (connection is null)
        {
            return Result.Failure<AdapterRemoteLeaseClaimResponse>(IngestionApplicationErrors.ConnectionNotFound);
        }

        if (connection.ExecutionMode != AdapterExecutionMode.RemotePolling)
        {
            return Result.Failure<AdapterRemoteLeaseClaimResponse>(
                BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.RemoteLeaseRequiresRemotePollingMode);
        }

        CountryPolicyDecision countryPolicyDecision = await countryPolicy.EvaluateAsync(
            connection.PropertyId,
            IngestionCountryPolicyAdmission.ReservationIngestionPurpose,
            CountryPolicySurface.AdapterIngress,
            IngestionCountryPolicyAdmission.ApprovedAdapterProvenance,
            cancellationToken).ConfigureAwait(false);
        if (!countryPolicyDecision.IsAllowed)
        {
            return Result.Failure<AdapterRemoteLeaseClaimResponse>(
                IngestionApplicationErrors.CountryPolicyDenied(countryPolicyDecision.Reason));
        }

        if (!descriptors.TryGet(connection.AdapterType, out AdapterDescriptor? descriptor) || descriptor is null ||
            !descriptor.ExecutionModes.Contains(AdapterExecutionMode.RemotePolling) ||
            !string.Equals(command.Request.AdapterType, descriptor.AdapterType, StringComparison.Ordinal) ||
            command.Request.ProtocolVersion != descriptor.ProtocolVersion ||
            command.Request.ConfigurationSchemaVersion != descriptor.ConfigurationSchemaVersion)
        {
            return Result.Failure<AdapterRemoteLeaseClaimResponse>(
                IngestionApplicationErrors.RemoteLeaseDescriptorMismatch);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        TimeSpan duration = TimeSpan.FromSeconds(command.Request.RequestedLeaseSeconds);
        IngestionRun? activeRun = await runs.FindActiveByConnectionAsync(
            connection.Id, cancellationToken).ConfigureAwait(false);
        if (activeRun is not null)
        {
            if (activeRun.ExecutionKind == IngestionRunExecutionKind.RemoteLease &&
                activeRun.RemoteLeaseExpiresAtUtc > nowUtc &&
                activeRun.RemoteCredentialId == command.CredentialId &&
                activeRun.RemoteWorkerId == command.Request.WorkerId &&
                activeRun.RemoteClaimId == command.Request.ClaimId &&
                connection.RemoteLeaseRunId == activeRun.Id &&
                connection.RemoteLeaseId == activeRun.RemoteLeaseId &&
                connection.RemoteLeaseClaimId == activeRun.RemoteClaimId &&
                connection.RemoteLeaseEpoch == activeRun.RemoteLeaseEpoch)
            {
                Result<DateTimeOffset> renewed = connection.RenewRemoteLease(
                    activeRun.Id,
                    activeRun.RemoteLeaseId!.Value,
                    activeRun.RemoteLeaseEpoch!.Value,
                    command.CredentialId,
                    command.Request.WorkerId,
                    duration,
                    connection.Version,
                    nowUtc);
                if (renewed.IsFailure)
                {
                    return Result.Failure<AdapterRemoteLeaseClaimResponse>(renewed.Error);
                }

                Result runRenewed = activeRun.RenewRemoteLease(
                    activeRun.RemoteLeaseId.Value,
                    activeRun.RemoteLeaseEpoch.Value,
                    command.CredentialId,
                    command.Request.WorkerId,
                    renewed.Value,
                    activeRun.Version,
                    nowUtc);
                if (runRenewed.IsFailure)
                {
                    return Result.Failure<AdapterRemoteLeaseClaimResponse>(runRenewed.Error);
                }

                AdapterRunAssignment existingAssignment = new(
                    activeRun.Id,
                    activeRun.RemoteLeaseId!.Value,
                    connection.Id,
                    connection.ScopeId,
                    connection.PropertyId,
                    connection.AdapterType,
                    AdapterExecutionMode.Polling,
                    activeRun.StartedAtUtc,
                    renewed.Value,
                    connection.Checkpoint);
                return Result.Success(new AdapterRemoteLeaseClaimResponse(
                    existingAssignment,
                    activeRun.RemoteLeaseEpoch!.Value,
                    RenewAfterSeconds(command.Request.RequestedLeaseSeconds)));
            }

            if (activeRun.ExecutionKind != IngestionRunExecutionKind.RemoteLease ||
                activeRun.RemoteLeaseExpiresAtUtc > nowUtc ||
                connection.RemoteLeaseRunId != activeRun.Id)
            {
                return Result.Failure<AdapterRemoteLeaseClaimResponse>(
                    IngestionApplicationErrors.RemoteLeaseUnavailable);
            }

            Result expired = activeRun.ExpireRemoteLease(connection.Checkpoint, activeRun.Version, nowUtc);
            if (expired.IsFailure)
            {
                return Result.Failure<AdapterRemoteLeaseClaimResponse>(expired.Error);
            }
        }

        Guid runId = ids.NewId();
        Guid leaseId = ids.NewId();
        Result<RemoteAdapterLeaseState> claimed = connection.ClaimRemoteLease(
            runId,
            leaseId,
            command.Request.ClaimId,
            command.CredentialId,
            command.Request.WorkerId,
            duration,
            connection.Version,
            nowUtc);
        if (claimed.IsFailure)
        {
            return Result.Failure<AdapterRemoteLeaseClaimResponse>(claimed.Error);
        }

        Result<IngestionRun> created = IngestionRun.StartRemote(
            runId,
            scopeContext.ScopeId,
            connection.Id,
            connection.PropertyId,
            leaseId,
            command.Request.ClaimId,
            claimed.Value.LeaseEpoch,
            command.CredentialId,
            command.Request.WorkerId,
            connection.Checkpoint,
            claimed.Value.ExpiresAtUtc,
            nowUtc);
        if (created.IsFailure)
        {
            return Result.Failure<AdapterRemoteLeaseClaimResponse>(created.Error);
        }

        await runs.AddAsync(created.Value, cancellationToken).ConfigureAwait(false);
        AdapterRunAssignment assignment = new(
            runId,
            leaseId,
            connection.Id,
            connection.ScopeId,
            connection.PropertyId,
            connection.AdapterType,
            AdapterExecutionMode.Polling,
            nowUtc,
            claimed.Value.ExpiresAtUtc,
            connection.Checkpoint);
        return Result.Success(new AdapterRemoteLeaseClaimResponse(
            assignment,
            claimed.Value.LeaseEpoch,
            RenewAfterSeconds(command.Request.RequestedLeaseSeconds)));
    }

    internal static int RenewAfterSeconds(int leaseSeconds) => Math.Max(10, leaseSeconds / 3);
}

internal sealed class RenewRemoteAdapterLeaseCommandHandler(
    IAdapterConnectionRepository connections,
    IIngestionRunRepository runs,
    IIngestionCountryPolicyAdmission countryPolicy,
    ISystemClock clock)
    : ICommandHandler<RenewRemoteAdapterLeaseCommand, AdapterRemoteLeaseRenewResponse>
{
    public async Task<Result<AdapterRemoteLeaseRenewResponse>> HandleAsync(
        RenewRemoteAdapterLeaseCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Request?.Lease is null ||
            command.Request.RequestedLeaseSeconds is < AdapterRemoteLeaseContractLimits.MinimumLeaseSeconds or
                > AdapterRemoteLeaseContractLimits.MaximumLeaseSeconds)
        {
            return Result.Failure<AdapterRemoteLeaseRenewResponse>(
                IngestionApplicationErrors.RemoteLeaseClaimInvalid);
        }

        AdapterConnection? connection = await connections.GetAsync(command.ConnectionId, cancellationToken)
            .ConfigureAwait(false);
        IngestionRun? run = await runs.GetAsync(command.Request.Lease.RunId, cancellationToken)
            .ConfigureAwait(false);
        if (connection is null || run is null || run.ConnectionId != connection.Id)
        {
            return Result.Failure<AdapterRemoteLeaseRenewResponse>(
                BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.RemoteLeaseMismatch);
        }

        CountryPolicyDecision countryPolicyDecision = await countryPolicy.EvaluateAsync(
            connection.PropertyId,
            IngestionCountryPolicyAdmission.ReservationIngestionPurpose,
            CountryPolicySurface.AdapterIngress,
            IngestionCountryPolicyAdmission.ApprovedAdapterProvenance,
            cancellationToken).ConfigureAwait(false);
        if (!countryPolicyDecision.IsAllowed)
        {
            return Result.Failure<AdapterRemoteLeaseRenewResponse>(
                IngestionApplicationErrors.CountryPolicyDenied(countryPolicyDecision.Reason));
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        TimeSpan duration = TimeSpan.FromSeconds(command.Request.RequestedLeaseSeconds);
        Result<DateTimeOffset> renewed = connection.RenewRemoteLease(
            run.Id,
            command.Request.Lease.LeaseId,
            command.Request.Lease.LeaseEpoch,
            command.CredentialId,
            command.Request.Lease.WorkerId,
            duration,
            connection.Version,
            nowUtc);
        if (renewed.IsFailure)
        {
            return Result.Failure<AdapterRemoteLeaseRenewResponse>(renewed.Error);
        }

        Result runRenewed = run.RenewRemoteLease(
            command.Request.Lease.LeaseId,
            command.Request.Lease.LeaseEpoch,
            command.CredentialId,
            command.Request.Lease.WorkerId,
            renewed.Value,
            run.Version,
            nowUtc);
        return runRenewed.IsSuccess
            ? Result.Success(new AdapterRemoteLeaseRenewResponse(
                run.Id,
                command.Request.Lease.LeaseId,
                command.Request.Lease.LeaseEpoch,
                renewed.Value,
                ClaimRemoteAdapterLeaseCommandHandler.RenewAfterSeconds(
                    command.Request.RequestedLeaseSeconds)))
            : Result.Failure<AdapterRemoteLeaseRenewResponse>(runRenewed.Error);
    }
}

internal sealed class CompleteRemoteAdapterRunCommandHandler(
    IAdapterConnectionRepository connections,
    IIngestionRunRepository runs,
    ISystemClock clock)
    : ICommandHandler<CompleteRemoteAdapterRunCommand, AdapterRemoteRunCompletionResponse>
{
    public async Task<Result<AdapterRemoteRunCompletionResponse>> HandleAsync(
        CompleteRemoteAdapterRunCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Request?.Lease is null)
        {
            return Result.Failure<AdapterRemoteRunCompletionResponse>(
                IngestionApplicationErrors.RemoteLeaseClaimInvalid);
        }

        AdapterConnection? connection = await connections.GetAsync(command.ConnectionId, cancellationToken)
            .ConfigureAwait(false);
        IngestionRun? run = await runs.GetAsync(command.Request.Lease.RunId, cancellationToken)
            .ConfigureAwait(false);
        if (connection is null || run is null || run.ConnectionId != connection.Id)
        {
            return Result.Failure<AdapterRemoteRunCompletionResponse>(
                BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.RemoteLeaseMismatch);
        }

        string? acceptedCheckpoint = Normalize(command.Request.AcceptedCheckpoint);
        string? errorCode = NormalizeErrorCode(command.Request.ErrorCode);
        if (run.State != IngestionRunState.Running)
        {
            return MatchesCompleted(run, command.CredentialId, command.Request, acceptedCheckpoint, errorCode)
                ? Result.Success(Map(run, command.Request.Lease))
                : Result.Failure<AdapterRemoteRunCompletionResponse>(
                    IngestionApplicationErrors.AdapterCompletionMismatch);
        }

        if (!string.Equals(acceptedCheckpoint, connection.Checkpoint, StringComparison.Ordinal))
        {
            return Result.Failure<AdapterRemoteRunCompletionResponse>(
                IngestionApplicationErrors.AdapterCompletionMismatch);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        Result completed = run.CompleteRemote(
            command.Request.Lease.LeaseId,
            command.Request.Lease.LeaseEpoch,
            command.CredentialId,
            command.Request.Lease.WorkerId,
            command.Request.Outcome,
            command.Request.ObservedCount,
            command.Request.AcceptedCount,
            command.Request.RejectedCount,
            acceptedCheckpoint,
            errorCode,
            run.Version,
            nowUtc);
        if (completed.IsFailure)
        {
            return Result.Failure<AdapterRemoteRunCompletionResponse>(completed.Error);
        }

        Result released = connection.ReleaseRemoteLease(
            run.Id,
            command.Request.Lease.LeaseId,
            command.Request.Lease.LeaseEpoch,
            command.CredentialId,
            command.Request.Lease.WorkerId,
            connection.Version,
            nowUtc);
        return released.IsSuccess
            ? Result.Success(Map(run, command.Request.Lease))
            : Result.Failure<AdapterRemoteRunCompletionResponse>(released.Error);
    }

    private static bool MatchesCompleted(
        IngestionRun run,
        Guid credentialId,
        AdapterRemoteRunCompletionRequest request,
        string? acceptedCheckpoint,
        string? errorCode) =>
        run.ExecutionKind == IngestionRunExecutionKind.RemoteLease &&
        run.RemoteLeaseId == request.Lease.LeaseId &&
        run.RemoteLeaseEpoch == request.Lease.LeaseEpoch &&
        run.RemoteCredentialId == credentialId &&
        run.RemoteWorkerId == request.Lease.WorkerId &&
        run.State == ToRunState(request.Outcome) &&
        run.ObservedCount == request.ObservedCount &&
        run.AcceptedCount == request.AcceptedCount &&
        run.RejectedCount == request.RejectedCount &&
        string.Equals(run.AcceptedCheckpoint, acceptedCheckpoint, StringComparison.Ordinal) &&
        string.Equals(run.ErrorCode, errorCode, StringComparison.Ordinal);

    private static AdapterRemoteRunCompletionResponse Map(
        IngestionRun run,
        AdapterRemoteLeaseProof lease) => new(
        run.Id,
        lease.LeaseId,
        lease.LeaseEpoch,
        ToRunOutcome(run.State),
        run.AcceptedCheckpoint,
        run.CompletedAtUtc ?? throw new InvalidOperationException("A completed remote run has no timestamp."));

    private static IngestionRunState ToRunState(AdapterRunOutcome outcome) => outcome switch
    {
        AdapterRunOutcome.Succeeded => IngestionRunState.Succeeded,
        AdapterRunOutcome.PartiallySucceeded => IngestionRunState.PartiallySucceeded,
        AdapterRunOutcome.Failed => IngestionRunState.Failed,
        AdapterRunOutcome.Cancelled => IngestionRunState.Cancelled,
        _ => IngestionRunState.Unknown
    };

    private static AdapterRunOutcome ToRunOutcome(IngestionRunState state) => state switch
    {
        IngestionRunState.Succeeded => AdapterRunOutcome.Succeeded,
        IngestionRunState.PartiallySucceeded => AdapterRunOutcome.PartiallySucceeded,
        IngestionRunState.Failed => AdapterRunOutcome.Failed,
        IngestionRunState.Cancelled => AdapterRunOutcome.Cancelled,
        _ => AdapterRunOutcome.Unknown
    };

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeErrorCode(string? errorCode)
        => Normalize(errorCode)?.ToLowerInvariant();
}
