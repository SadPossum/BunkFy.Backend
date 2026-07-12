namespace BunkFy.Modules.Ingestion.Application.Commands;

using BunkFy.Adapter.Abstractions;
using Gma.Framework.Cqrs;

public sealed record ReceiveObservationCommand(
    Guid ConnectionId,
    Guid? RunId,
    Guid OperationId,
    string RecordType,
    string ExternalRecordId,
    string? SourceRevision,
    DateTimeOffset? SourceUpdatedAtUtc,
    DateTimeOffset ObservedAtUtc,
    string ContentType,
    ReadOnlyMemory<byte> Payload,
    string ContentSha256,
    Guid? SourceReceiptId = null,
    Guid? ReprocessingAttemptId = null,
    string? ParserType = null,
    int? ParserVersion = null,
    int? ParserOutputIndex = null,
    AdapterRemoteLeaseProof? RemoteLease = null,
    Guid? RemoteCredentialId = null)
    : ITransactionalCommand<AdapterObservationResult>;
