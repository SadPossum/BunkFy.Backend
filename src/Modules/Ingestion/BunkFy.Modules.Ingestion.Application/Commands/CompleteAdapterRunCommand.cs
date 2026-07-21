namespace BunkFy.Modules.Ingestion.Application.Commands;

using BunkFy.Adapter.Abstractions;
using Gma.Framework.Cqrs;

public sealed record CompleteAdapterRunCommand(
    Guid RunId,
    Guid TaskRunId,
    int TaskAttempt,
    AdapterRunOutcome Outcome,
    int ObservedCount,
    int AcceptedCount,
    int RejectedCount,
    string? AcceptedCheckpoint,
    string? ErrorCode)
    : ITransactionalCommand<Unit>;
