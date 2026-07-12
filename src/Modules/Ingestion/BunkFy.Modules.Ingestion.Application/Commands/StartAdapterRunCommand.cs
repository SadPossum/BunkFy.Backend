namespace BunkFy.Modules.Ingestion.Application.Commands;

using BunkFy.Adapter.Abstractions;
using Gma.Framework.Cqrs;

public sealed record StartAdapterRunCommand(
    Guid ConnectionId,
    Guid TaskRunId,
    int TaskAttempt)
    : ITransactionalCommand<AdapterRunStart>;

public sealed record AdapterRunStart(
    Guid RunId,
    Guid ConnectionId,
    Guid PropertyId,
    string ScopeId,
    string AdapterType,
    AdapterExecutionMode ExecutionMode,
    string? Checkpoint,
    string ConfigurationReference,
    string? SecretReference);
