namespace BunkFy.Modules.Ingestion.Application.Commands;

using BunkFy.Adapter.Abstractions;
using Gma.Framework.Cqrs;
using BunkFy.Modules.Ingestion.Contracts;

public sealed record CreateAdapterConnectionCommand(
    Guid PropertyId,
    string AdapterType,
    AdapterExecutionMode ExecutionMode,
    AdapterConflictPolicy ConflictPolicy,
    string ConfigurationReference,
    string? SecretReference) : ITransactionalCommand<AdapterConnectionDto>;

public sealed record UpdateAdapterConnectionCommand(
    Guid PropertyId,
    Guid ConnectionId,
    AdapterExecutionMode ExecutionMode,
    AdapterConflictPolicy ConflictPolicy,
    string ConfigurationReference,
    SecretReferenceUpdateMode SecretReferenceUpdateMode,
    string? SecretReference,
    long ExpectedVersion) : ITransactionalCommand<AdapterConnectionDto>;

public enum SecretReferenceUpdateMode
{
    Unknown = 0,
    Keep = 1,
    Replace = 2,
    Clear = 3
}

public sealed record ConfigureAdapterConnectionPollingScheduleCommand(
    Guid PropertyId,
    Guid ConnectionId,
    int IntervalSeconds,
    int MaxAttempts,
    long ExpectedVersion) : ITransactionalCommand<AdapterConnectionDto>;

public sealed record ClearAdapterConnectionPollingScheduleCommand(
    Guid PropertyId,
    Guid ConnectionId,
    long ExpectedVersion) : ITransactionalCommand<AdapterConnectionDto>;

public sealed record SetAdapterConnectionEnabledCommand(
    Guid PropertyId,
    Guid ConnectionId,
    bool Enabled,
    long ExpectedVersion) : ITransactionalCommand<AdapterConnectionDto>;

public sealed record ResetAdapterConnectionCheckpointCommand(
    Guid PropertyId,
    Guid ConnectionId,
    long ExpectedVersion) : ITransactionalCommand<AdapterConnectionDto>;
