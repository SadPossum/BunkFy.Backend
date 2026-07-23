namespace BunkFy.Modules.Ingestion.Application.Handlers;

using BunkFy.DataGovernance;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Application.Adapters;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Application.Policies;
using BunkFy.Modules.Ingestion.Domain.Connections;
using BunkFy.Modules.Ingestion.Domain.Runs;

internal sealed class StartAdapterRunCommandHandler(
    IAdapterConnectionRepository connections,
    IIngestionCountryPolicyAdmission countryPolicy,
    IIngestionRunRepository runs,
    IAdapterDescriptorRegistry descriptors,
    IScopeContext scopeContext,
    IIdGenerator idGenerator,
    ISystemClock clock)
    : ICommandHandler<StartAdapterRunCommand, AdapterRunStart>
{
    public async Task<Result<AdapterRunStart>> HandleAsync(
        StartAdapterRunCommand command,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled || string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<AdapterRunStart>(IngestionApplicationErrors.ScopeRequired);
        }

        AdapterConnection? connection = await connections.GetAsync(command.ConnectionId, cancellationToken)
            .ConfigureAwait(false);
        if (connection is null)
        {
            return Result.Failure<AdapterRunStart>(IngestionApplicationErrors.ConnectionNotFound);
        }

        if (connection.State != AdapterConnectionState.Enabled)
        {
            return Result.Failure<AdapterRunStart>(IngestionApplicationErrors.ConnectionNotEnabled);
        }

        Result capability = AdapterCapabilityValidation.Validate(
            descriptors, connection.AdapterType, connection.ExecutionMode);
        if (capability.IsFailure)
        {
            return Result.Failure<AdapterRunStart>(capability.Error);
        }

        if (connection.ExecutionMode is
            BunkFy.Adapter.Abstractions.AdapterExecutionMode.Push or
            BunkFy.Adapter.Abstractions.AdapterExecutionMode.RemotePolling)
        {
            return Result.Failure<AdapterRunStart>(
                IngestionApplicationErrors.AdapterExecutionModeNotTaskRunnable);
        }

        CountryPolicyDecision countryPolicyDecision = await countryPolicy.EvaluateAsync(
            connection.PropertyId,
            IngestionCountryPolicyAdmission.ReservationIngestionPurpose,
            CountryPolicySurface.AdapterIngress,
            IngestionCountryPolicyAdmission.ApprovedAdapterProvenance,
            cancellationToken).ConfigureAwait(false);
        if (!countryPolicyDecision.IsAllowed)
        {
            return Result.Failure<AdapterRunStart>(
                IngestionApplicationErrors.CountryPolicyDenied(countryPolicyDecision.Reason));
        }

        IngestionRun? existing = await runs.FindByTaskExecutionAsync(
            command.TaskRunId,
            command.TaskAttempt,
            cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing.ConnectionId == connection.Id && existing.State == IngestionRunState.Running
                ? Result.Success(Map(existing, connection))
                : Result.Failure<AdapterRunStart>(IngestionApplicationErrors.TaskContextMismatch);
        }

        if (await runs.FindActiveByConnectionAsync(connection.Id, cancellationToken).ConfigureAwait(false) is not null)
        {
            return Result.Failure<AdapterRunStart>(IngestionApplicationErrors.ConnectionRunAlreadyActive);
        }

        Result<IngestionRun> created = IngestionRun.Start(
            idGenerator.NewId(),
            scopeContext.ScopeId,
            connection.Id,
            connection.PropertyId,
            command.TaskRunId,
            command.TaskAttempt,
            connection.Checkpoint,
            clock.UtcNow);
        if (created.IsFailure)
        {
            return Result.Failure<AdapterRunStart>(created.Error);
        }

        await runs.AddAsync(created.Value, cancellationToken).ConfigureAwait(false);
        return Result.Success(Map(created.Value, connection));
    }

    private static AdapterRunStart Map(IngestionRun run, AdapterConnection connection) => new(
        run.Id,
        connection.Id,
        connection.PropertyId,
        run.ScopeId,
        connection.AdapterType,
        connection.ExecutionMode,
        run.StartingCheckpoint,
        connection.ConfigurationReference,
        connection.SecretReference);
}
