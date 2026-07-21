namespace BunkFy.Modules.Ingestion.Application.Tasks;

using BunkFy.Adapter.Abstractions;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Tasks;
using Gma.Framework.Tasks.Cqrs;
using BunkFy.Modules.Ingestion.Application.Adapters;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Ingestion.Contracts.Adapters;

internal sealed class RunAdapterTaskHandler(
    ITaskCommandDispatcher commandDispatcher,
    IAdapterDescriptorRegistry descriptors,
    IAdapterRunnerRegistry runners,
    IAdapterConfigurationMaterialResolver materialResolver,
    IAdapterObservationSinkFactory sinkFactory,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ITaskHandler<RunAdapterTaskPayload>
{
    private static readonly TimeSpan DefaultAssignmentLease = TimeSpan.FromMinutes(5);

    public async Task HandleAsync(
        RunAdapterTaskPayload payload,
        TaskExecutionContext context,
        CancellationToken cancellationToken)
    {
        Result<AdapterRunStart> start = await commandDispatcher.DispatchAsync<StartAdapterRunCommand, AdapterRunStart>(
            context,
            new StartAdapterRunCommand(payload.ConnectionId, context.RunId, context.Attempt),
            cancellationToken).ConfigureAwait(false);
        if (start.IsFailure)
        {
            throw new InvalidOperationException($"{start.Error.Code}: {start.Error.Message}");
        }

        if (!runners.TryGet(start.Value.AdapterType, out IAdapterRunner? runner) || runner is null)
        {
            await this.RecordFailureAsync(
                start.Value,
                context,
                IngestionApplicationErrors.AdapterRunnerNotRegistered.Code,
                cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException(IngestionApplicationErrors.AdapterRunnerNotRegistered.Code);
        }

        if (!descriptors.TryGet(start.Value.AdapterType, out AdapterDescriptor? registered) ||
            registered is null ||
            registered.ProtocolVersion != runner.Descriptor.ProtocolVersion ||
            registered.ConfigurationSchemaVersion != runner.Descriptor.ConfigurationSchemaVersion ||
            !registered.ExecutionModes.Order().SequenceEqual(runner.Descriptor.ExecutionModes.Order()) ||
            registered.Polling?.MinimumInterval != runner.Descriptor.Polling?.MinimumInterval ||
            registered.Polling?.RecommendedInterval != runner.Descriptor.Polling?.RecommendedInterval)
        {
            await this.RecordFailureAsync(
                start.Value,
                context,
                IngestionApplicationErrors.AdapterDescriptorMismatch.Code,
                cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException(IngestionApplicationErrors.AdapterDescriptorMismatch.Code);
        }

        if (!runner.Descriptor.ExecutionModes.Contains(start.Value.ExecutionMode))
        {
            await this.RecordFailureAsync(
                start.Value,
                context,
                IngestionApplicationErrors.AdapterExecutionModeUnsupported.Code,
                cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException(IngestionApplicationErrors.AdapterExecutionModeUnsupported.Code);
        }

        DateTimeOffset assignedAtUtc = clock.UtcNow;
        AdapterRunAssignment assignment = new(
            start.Value.RunId,
            idGenerator.NewId(),
            start.Value.ConnectionId,
            start.Value.ScopeId,
            start.Value.PropertyId,
            start.Value.AdapterType,
            start.Value.ExecutionMode,
            assignedAtUtc,
            assignedAtUtc.Add(context.LeaseExtension ?? DefaultAssignmentLease),
            start.Value.Checkpoint);

        AdapterRunCompletion completion;
        string? declaredFailureCode = null;
        try
        {
            Result<AdapterConfigurationMaterial> resolvedMaterial = await materialResolver.ResolveAsync(
                new AdapterConfigurationMaterialRequest(
                    start.Value.ConnectionId,
                    start.Value.ScopeId,
                    start.Value.PropertyId,
                    start.Value.AdapterType,
                    runner.Descriptor.ConfigurationSchemaVersion,
                    start.Value.ConfigurationReference,
                    start.Value.SecretReference),
                cancellationToken).ConfigureAwait(false);
            if (resolvedMaterial.IsFailure)
            {
                declaredFailureCode = resolvedMaterial.Error.Code;
                throw new InvalidOperationException(resolvedMaterial.Error.Code);
            }

            using AdapterConfigurationMaterial material = resolvedMaterial.Value;
            completion = await runner.RunAsync(
                assignment,
                material,
                sinkFactory.Create(assignment),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await this.RecordTerminalAsync(
                start.Value,
                context,
                AdapterRunOutcome.Cancelled,
                "ingestion.adapter-execution-cancelled",
                CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        catch (Exception)
        {
            string failureCode = declaredFailureCode ?? "ingestion.adapter-execution-failed";
            await this.RecordFailureAsync(
                start.Value,
                context,
                failureCode,
                cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException(NormalizeErrorCode(failureCode));
        }

        if (completion.RunId != assignment.RunId || completion.LeaseId != assignment.LeaseId)
        {
            await this.RecordFailureAsync(
                start.Value,
                context,
                IngestionApplicationErrors.AdapterCompletionMismatch.Code,
                cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException(IngestionApplicationErrors.AdapterCompletionMismatch.Code);
        }

        Result<Unit> recorded = await commandDispatcher.DispatchAsync<CompleteAdapterRunCommand, Unit>(
            context,
            new CompleteAdapterRunCommand(
                start.Value.RunId,
                context.RunId,
                context.Attempt,
                completion.Outcome,
                completion.ObservedCount,
                completion.AcceptedCount,
                completion.RejectedCount,
                completion.AcceptedCheckpoint,
                completion.ErrorCode),
            cancellationToken).ConfigureAwait(false);
        if (recorded.IsFailure)
        {
            throw new InvalidOperationException($"{recorded.Error.Code}: {recorded.Error.Message}");
        }

        if (completion.Outcome == AdapterRunOutcome.Failed)
        {
            throw new InvalidOperationException(completion.ErrorCode ?? "ingestion.adapter-failed");
        }

        if (completion.Outcome == AdapterRunOutcome.Cancelled)
        {
            throw new TaskRunCanceledException(completion.ErrorCode ?? "ingestion.adapter-execution-cancelled");
        }
    }

    private Task<Result<Unit>> RecordFailureAsync(
        AdapterRunStart start,
        TaskExecutionContext context,
        string errorCode,
        CancellationToken cancellationToken) =>
        this.RecordTerminalAsync(
            start,
            context,
            AdapterRunOutcome.Failed,
            errorCode,
            cancellationToken);

    private Task<Result<Unit>> RecordTerminalAsync(
        AdapterRunStart start,
        TaskExecutionContext context,
        AdapterRunOutcome outcome,
        string errorCode,
        CancellationToken cancellationToken) =>
        commandDispatcher.DispatchAsync<CompleteAdapterRunCommand, Unit>(
            context,
            new CompleteAdapterRunCommand(
                start.RunId,
                context.RunId,
                context.Attempt,
                outcome,
                0,
                0,
                0,
                AcceptedCheckpoint: null,
                NormalizeErrorCode(errorCode)),
            cancellationToken);

    private static string NormalizeErrorCode(string? errorCode)
    {
        string normalized = string.IsNullOrWhiteSpace(errorCode)
            ? "ingestion.adapter-execution-failed"
            : errorCode.Trim().ToLowerInvariant();
        bool valid = normalized.Length <= AdapterProtocolLimits.ErrorCodeMaxLength &&
                     char.IsAsciiLetterOrDigit(normalized[0]) &&
                     normalized.All(character =>
                         char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_');
        return valid ? normalized : "ingestion.adapter-execution-failed";
    }
}
