namespace BunkFy.Modules.Properties.Application.Commands;

using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Cqrs;

public sealed record ActivatePropertyProcessingCommand(
    Guid PropertyId,
    string OperatingCountryCode,
    string PolicyId,
    int PolicyVersion,
    string DataRegionId,
    string TransferProfileId,
    string RetentionPolicyId,
    int RetentionPolicyVersion,
    IReadOnlyCollection<PropertyGovernanceAcknowledgementDto> AcceptedAcknowledgements,
    bool Confirmed,
    long ExpectedVersion,
    string ActorId)
    : ITransactionalCommand<PropertyDto>;
