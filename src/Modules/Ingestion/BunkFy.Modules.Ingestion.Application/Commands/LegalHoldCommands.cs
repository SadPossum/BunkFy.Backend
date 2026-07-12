namespace BunkFy.Modules.Ingestion.Application.Commands;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Ingestion.Contracts;

public sealed record PlaceLegalHoldCommand(
    Guid PropertyId,
    string Reason,
    string PlacedBy)
    : ITransactionalCommand<LegalHoldDto>;

public sealed record ReleaseLegalHoldCommand(
    Guid PropertyId,
    Guid HoldId,
    long ExpectedVersion,
    string ReleaseReason,
    string ReleasedBy)
    : ITransactionalCommand<LegalHoldDto>;
