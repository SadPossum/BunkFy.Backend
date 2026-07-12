namespace BunkFy.Modules.Ingestion.Application.Tasks;

using BunkFy.Adapter.Abstractions;
using BunkFy.ObservationParsing;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Tasks;
using Gma.Framework.Tasks.Cqrs;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Application.Parsing;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Ingestion.Domain.Reprocessing;

internal sealed class ReprocessObservationTaskHandler(
    ITaskCommandDispatcher commandDispatcher,
    IObservationParserDescriptorRegistry descriptors,
    IObservationParserRegistry parsers,
    IRawPayloadStore rawPayloads)
    : ITaskHandler<ReprocessObservationPayload>
{
    public async Task HandleAsync(
        ReprocessObservationPayload payload,
        TaskExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (payload.AttemptId == Guid.Empty || payload.AttemptId != context.RunId ||
            payload.ParserVersion <= 0 || payload.MaxAttempts is <= 0 or > ReprocessObservationPayload.MaximumAttempts ||
            context.Attempt > payload.MaxAttempts)
        {
            throw new InvalidOperationException(IngestionApplicationErrors.TaskContextMismatch.Code);
        }

        bool started = false;
        try
        {
            Result<ObservationReprocessingStart> start = await commandDispatcher.DispatchAsync<
                StartObservationReprocessingCommand,
                ObservationReprocessingStart>(
                context,
                new StartObservationReprocessingCommand(payload.AttemptId, context.RunId, context.Attempt),
                cancellationToken).ConfigureAwait(false);
            if (start.IsFailure)
            {
                throw new InvalidOperationException(start.Error.Code);
            }

            started = true;
            if (!string.Equals(start.Value.ParserType, payload.ParserType, StringComparison.Ordinal) ||
                start.Value.ParserVersion != payload.ParserVersion)
            {
                await this.FailAsync(context, payload.AttemptId,
                    IngestionApplicationErrors.ReprocessingParserMismatch.Code, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (!descriptors.TryGet(payload.ParserType, payload.ParserVersion, out ObservationParserDescriptor? descriptor) ||
                descriptor is null ||
                !parsers.TryGet(payload.ParserType, payload.ParserVersion, out IObservationParser? parser) ||
                parser is null || !Matches(descriptor, parser.Descriptor))
            {
                throw new InvalidOperationException(IngestionApplicationErrors.ReprocessingParserMismatch.Code);
            }

            RawPayloadRead? raw = await rawPayloads.ReadAsync(
                start.Value.RawPayloadFileId,
                start.Value.ScopeId,
                start.Value.ConnectionId,
                cancellationToken).ConfigureAwait(false);
            if (raw is null || !string.Equals(raw.ContentSha256, start.Value.ContentHash,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(AdapterPayloadHash.ComputeSha256(raw.Content.Span), start.Value.ContentHash,
                    StringComparison.OrdinalIgnoreCase))
            {
                await this.FailAsync(context, payload.AttemptId,
                    IngestionApplicationErrors.ReprocessingRawPayloadInvalid.Code, cancellationToken).ConfigureAwait(false);
                return;
            }

            byte[] inputBytes = raw.Content.ToArray();
            try
            {
                using ObservationParserInput input = new(
                    start.Value.SourceReceiptId,
                    start.Value.AdapterType,
                    start.Value.SourceRecordType,
                    start.Value.ExternalId,
                    start.Value.SourceRevision,
                    start.Value.SourceUpdatedAtUtc,
                    start.Value.ObservedAtUtc,
                    raw.ContentType,
                    inputBytes,
                    raw.ContentSha256);
                using ObservationParserResult parsed = await parser.ParseAsync(input, cancellationToken)
                    .ConfigureAwait(false);
                if (parsed.Disposition == ObservationParserDisposition.NoMatch)
                {
                    await this.CompleteAsync(
                        context, payload.AttemptId, 0, 0, 0, 0, true, parsed.ReasonCode,
                        cancellationToken).ConfigureAwait(false);
                    return;
                }

                if (!OutputsAreValid(parsed, descriptor))
                {
                    await this.CompleteAsync(
                        context,
                        payload.AttemptId,
                        parsed.Outputs.Count,
                        0,
                        0,
                        parsed.Outputs.Count,
                        false,
                        IngestionApplicationErrors.ReprocessingOutputInvalid.Code,
                        cancellationToken).ConfigureAwait(false);
                    return;
                }

                int accepted = 0;
                int duplicate = 0;
                int rejected = 0;
                string? rejectionCode = null;
                int outputIndex = 0;
                foreach (ParsedObservation output in parsed.Outputs)
                {
                    Guid operationId = ObservationIdentity.CreateReprocessingOperationId(
                        start.Value.SourceReceiptId,
                        payload.ParserType,
                        payload.ParserVersion,
                        outputIndex,
                        output.RecordType,
                        output.ExternalRecordId,
                        output.SourceRevision,
                        output.ContentSha256);
                    Result<AdapterObservationResult> received = await commandDispatcher.DispatchAsync<
                        ReceiveObservationCommand,
                        AdapterObservationResult>(
                        context,
                        new ReceiveObservationCommand(
                            start.Value.ConnectionId,
                            RunId: null,
                            operationId,
                            output.RecordType,
                            output.ExternalRecordId,
                            output.SourceRevision,
                            output.SourceUpdatedAtUtc,
                            output.ObservedAtUtc,
                            output.ContentType,
                            output.Payload,
                            output.ContentSha256,
                            start.Value.SourceReceiptId,
                            start.Value.AttemptId,
                            payload.ParserType,
                            payload.ParserVersion,
                            outputIndex),
                        cancellationToken).ConfigureAwait(false);
                    if (received.IsFailure)
                    {
                        rejected++;
                        rejectionCode ??= received.Error.Code;
                    }
                    else if (received.Value.Disposition == AdapterObservationDisposition.Accepted)
                    {
                        accepted++;
                    }
                    else if (received.Value.Disposition == AdapterObservationDisposition.Duplicate)
                    {
                        duplicate++;
                    }
                    else
                    {
                        rejected++;
                        rejectionCode ??= IngestionApplicationErrors.ReprocessingOutputInvalid.Code;
                    }

                    ObservationReprocessingOutputDisposition disposition = received.IsFailure
                        ? ObservationReprocessingOutputDisposition.Rejected
                        : received.Value.Disposition == AdapterObservationDisposition.Accepted
                            ? ObservationReprocessingOutputDisposition.Accepted
                            : received.Value.Disposition == AdapterObservationDisposition.Duplicate
                                ? ObservationReprocessingOutputDisposition.Duplicate
                                : ObservationReprocessingOutputDisposition.Rejected;
                    Result<Unit> recorded = await commandDispatcher.DispatchAsync<
                        RecordObservationReprocessingOutputCommand,
                        Unit>(
                        context,
                        new RecordObservationReprocessingOutputCommand(
                            start.Value.AttemptId,
                            outputIndex,
                            operationId,
                            received.IsSuccess ? received.Value.ReceiptId : null,
                            disposition,
                            output.RecordType,
                            output.ExternalRecordId,
                            output.SourceRevision,
                            output.ContentSha256,
                            disposition == ObservationReprocessingOutputDisposition.Rejected
                                ? received.IsFailure
                                    ? received.Error.Code
                                    : IngestionApplicationErrors.ReprocessingOutputInvalid.Code
                                : null),
                        cancellationToken).ConfigureAwait(false);
                    if (recorded.IsFailure)
                    {
                        throw new InvalidOperationException(recorded.Error.Code);
                    }

                    outputIndex++;
                }

                await this.CompleteAsync(
                    context,
                    payload.AttemptId,
                    parsed.Outputs.Count,
                    accepted,
                    duplicate,
                    rejected,
                    false,
                    rejectionCode,
                    cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(inputBytes);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (started)
            {
                _ = await commandDispatcher.DispatchAsync<CancelObservationReprocessingCommand, Unit>(
                    context,
                    new CancelObservationReprocessingCommand(payload.AttemptId),
                    CancellationToken.None).ConfigureAwait(false);
            }

            throw new TaskRunCanceledException("Observation reprocessing was canceled.");
        }
        catch (Exception)
        {
            const string errorCode = "ingestion.reprocessing-execution-failed";
            if (context.Attempt < payload.MaxAttempts && started)
            {
                _ = await commandDispatcher.DispatchAsync<ScheduleObservationReprocessingRetryCommand, Unit>(
                    context,
                    new ScheduleObservationReprocessingRetryCommand(payload.AttemptId, context.Attempt, errorCode),
                    CancellationToken.None).ConfigureAwait(false);
            }
            else if (context.Attempt >= payload.MaxAttempts)
            {
                _ = await commandDispatcher.DispatchAsync<FailPreparedObservationReprocessingCommand, Unit>(
                    context,
                    new FailPreparedObservationReprocessingCommand(payload.AttemptId, errorCode),
                    CancellationToken.None).ConfigureAwait(false);
            }

            throw;
        }
    }

    private async Task FailAsync(
        TaskExecutionContext context,
        Guid attemptId,
        string errorCode,
        CancellationToken cancellationToken)
    {
        Result<Unit> result = await commandDispatcher.DispatchAsync<FailPreparedObservationReprocessingCommand, Unit>(
            context,
            new FailPreparedObservationReprocessingCommand(attemptId, errorCode),
            cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            throw new InvalidOperationException(result.Error.Code);
        }
    }

    private async Task CompleteAsync(
        TaskExecutionContext context,
        Guid attemptId,
        int parsed,
        int accepted,
        int duplicate,
        int rejected,
        bool noMatch,
        string? reasonCode,
        CancellationToken cancellationToken)
    {
        Result<Unit> result = await commandDispatcher.DispatchAsync<CompleteObservationReprocessingCommand, Unit>(
            context,
            new CompleteObservationReprocessingCommand(
                attemptId, parsed, accepted, duplicate, rejected, noMatch, reasonCode),
            cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            throw new InvalidOperationException(result.Error.Code);
        }
    }

    private static bool Matches(ObservationParserDescriptor registered, ObservationParserDescriptor executable) =>
        registered.ParserType == executable.ParserType &&
        registered.ParserVersion == executable.ParserVersion &&
        registered.SupportedAdapterTypes.SequenceEqual(executable.SupportedAdapterTypes, StringComparer.Ordinal) &&
        registered.SupportedSourceRecordTypes.SequenceEqual(
            executable.SupportedSourceRecordTypes, StringComparer.Ordinal) &&
        registered.OutputRecordTypes.SequenceEqual(executable.OutputRecordTypes, StringComparer.Ordinal);

    private static bool OutputsAreValid(
        ObservationParserResult result,
        ObservationParserDescriptor descriptor)
    {
        if (result.Disposition != ObservationParserDisposition.Parsed || result.Outputs.Count == 0 ||
            result.Outputs.Any(output => !descriptor.OutputRecordTypes.Contains(
                output.RecordType,
                StringComparer.Ordinal)))
        {
            return false;
        }

        return result.Outputs
            .Select(output => ObservationIdentity.CreateDeduplicationKey(
                output.RecordType,
                output.ExternalRecordId,
                output.SourceRevision,
                output.ContentSha256))
            .Distinct(StringComparer.Ordinal)
            .Count() == result.Outputs.Count;
    }
}
