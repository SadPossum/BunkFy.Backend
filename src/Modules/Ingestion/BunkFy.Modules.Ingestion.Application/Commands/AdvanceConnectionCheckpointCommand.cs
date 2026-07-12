namespace BunkFy.Modules.Ingestion.Application.Commands;

using BunkFy.Adapter.Abstractions;
using Gma.Framework.Cqrs;

public sealed record AdvanceConnectionCheckpointCommand(
    Guid ConnectionId,
    Guid RunId,
    string Checkpoint,
    AdapterRemoteLeaseProof? RemoteLease = null,
    Guid? RemoteCredentialId = null)
    : ITransactionalCommand<Unit>;
