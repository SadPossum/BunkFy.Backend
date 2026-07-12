namespace BunkFy.Modules.Ingestion.Application.Commands;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Ingestion.Application.Ports;

public sealed record ClaimExpiredRawPayloadsCommand(
    Guid ClaimId,
    int BatchSize,
    int StaleClaimMinutes) : ITransactionalCommand<IReadOnlyList<RawPayloadPurgeCandidate>>;

public sealed record CompleteRawPayloadPurgeCommand(
    Guid ReceiptId,
    Guid ClaimId) : ITransactionalCommand<Unit>;
