namespace BunkFy.Modules.Properties.Application.Handlers;

using System.Security.Cryptography;
using System.Text;
using BunkFy.DataGovernance;
using BunkFy.Modules.Properties.Application.Commands;
using BunkFy.Modules.Properties.Application.Mapping;
using BunkFy.Modules.Properties.Application.Ports;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Properties.Domain.Aggregates;
using BunkFy.Modules.Properties.Domain.Errors;
using BunkFy.Modules.Properties.Domain.ValueObjects;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using DomainAcknowledgement = BunkFy.Modules.Properties.Domain.ValueObjects.PropertyGovernanceAcknowledgement;

internal sealed class ActivatePropertyProcessingCommandHandler(
    IPropertyRepository properties,
    IPropertyGovernanceRevisionWriter revisions,
    CountryPolicyRegistry countryPolicies,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ICommandHandler<ActivatePropertyProcessingCommand, PropertyDto>
{
    internal const string AccommodationType = "hostel";
    internal const string ActivationPurpose = "property-activation";
    internal const string OperatorProvenance = "authorized-workspace-operator";

    public async Task<Result<PropertyDto>> HandleAsync(
        ActivatePropertyProcessingCommand command,
        CancellationToken cancellationToken)
    {
        if (!command.Confirmed)
        {
            return Result.Failure<PropertyDto>(PropertiesApplicationErrors.ConfirmationRequired);
        }

        Property? property = await properties.GetAsync(command.PropertyId, cancellationToken).ConfigureAwait(false);
        if (property is null)
        {
            return Result.Failure<PropertyDto>(PropertiesDomainErrors.PropertyNotFound);
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        CountryPolicyAcknowledgement[] requestedAcknowledgements = command.AcceptedAcknowledgements?
            .Select(acknowledgement => new CountryPolicyAcknowledgement(
                acknowledgement.AcknowledgementId,
                acknowledgement.AcknowledgementVersion))
            .ToArray() ?? [];
        CountryPolicyDecision decision = countryPolicies.EvaluateActivation(new CountryPolicyActivationRequest(
            command.OperatingCountryCode,
            command.PolicyId,
            command.PolicyVersion,
            command.DataRegionId,
            command.TransferProfileId,
            command.RetentionPolicyId,
            command.RetentionPolicyVersion,
            requestedAcknowledgements,
            AccommodationType,
            ActivationPurpose,
            OperatorProvenance,
            nowUtc));
        if (!decision.IsAllowed || decision.Evidence is null)
        {
            return Result.Failure<PropertyDto>(PropertiesApplicationErrors.CountryPolicyDenied(decision.Reason));
        }

        Result<PropertyGovernanceBinding> bindingResult = CreateBinding(decision.Evidence);
        if (bindingResult.IsFailure)
        {
            return Result.Failure<PropertyDto>(bindingResult.Error);
        }

        Result<IReadOnlyCollection<DomainAcknowledgement>> acknowledgementResult =
            CreateAcknowledgements(decision.Evidence.AcceptedAcknowledgements);
        if (acknowledgementResult.IsFailure)
        {
            return Result.Failure<PropertyDto>(acknowledgementResult.Error);
        }

        PropertyGovernanceRevisionCoordinates? previous = ToCoordinates(
            property.GovernanceBinding,
            property.GovernanceAcknowledgements);
        PropertyProcessingState previousState = property.ProcessingState;
        Result activation = property.ActivateProcessing(
            bindingResult.Value,
            acknowledgementResult.Value,
            command.ExpectedVersion,
            idGenerator.NewId(),
            nowUtc,
            command.ActorId);
        if (activation.IsFailure)
        {
            return Result.Failure<PropertyDto>(activation.Error);
        }

        PropertyGovernanceRevisionAction action = previous is null
            ? PropertyGovernanceRevisionAction.Activated
            : previousState == PropertyProcessingState.Suspended &&
              previous == ToCoordinates(property.GovernanceBinding, property.GovernanceAcknowledgements)
                ? PropertyGovernanceRevisionAction.Reactivated
                : PropertyGovernanceRevisionAction.Rebound;
        await revisions.AppendAsync(
            new PropertyGovernanceRevisionWriteModel(
                idGenerator.NewId(),
                property.ScopeId,
                property.Id,
                property.Version,
                action,
                CountryPolicyDecisionReason.Allowed.ToString(),
                previous,
                ToCoordinates(property.GovernanceBinding, property.GovernanceAcknowledgements),
                command.ActorId.Trim(),
                nowUtc),
            cancellationToken).ConfigureAwait(false);

        return Result.Success(PropertiesMapper.ToDto(property));
    }

    private static Result<PropertyGovernanceBinding> CreateBinding(CountryPolicyEvidence evidence) =>
        PropertyGovernanceBinding.Create(
            evidence.OperatingCountryCode,
            evidence.PolicyId,
            evidence.PolicyVersion,
            evidence.DataRegionId,
            evidence.TransferProfileId,
            evidence.RetentionPolicyId,
            evidence.RetentionPolicyVersion,
            evidence.ContentSha256,
            evidence.EffectiveAtUtc,
            evidence.ExpiresAtUtc,
            evidence.EvaluatedAtUtc);

    private static Result<IReadOnlyCollection<DomainAcknowledgement>> CreateAcknowledgements(
        IReadOnlyCollection<CountryPolicyAcknowledgement> acknowledgements)
    {
        List<DomainAcknowledgement> values = [];
        foreach (CountryPolicyAcknowledgement acknowledgement in acknowledgements)
        {
            Result<DomainAcknowledgement> result = DomainAcknowledgement.Create(
                acknowledgement.AcknowledgementId,
                acknowledgement.AcknowledgementVersion);
            if (result.IsFailure)
            {
                return Result.Failure<IReadOnlyCollection<DomainAcknowledgement>>(result.Error);
            }

            values.Add(result.Value);
        }

        return Result.Success<IReadOnlyCollection<DomainAcknowledgement>>(values);
    }

    internal static PropertyGovernanceRevisionCoordinates? ToCoordinates(
        PropertyGovernanceBinding? binding,
        IReadOnlyCollection<DomainAcknowledgement> acknowledgements) =>
        binding is null
            ? null
            : new PropertyGovernanceRevisionCoordinates(
                binding.OperatingCountryCode,
                binding.PolicyId,
                binding.PolicyVersion,
                binding.DataRegionId,
                binding.TransferProfileId,
                binding.RetentionPolicyId,
                binding.RetentionPolicyVersion,
                binding.ContentSha256,
                HashAcknowledgements(acknowledgements));

    private static string HashAcknowledgements(
        IReadOnlyCollection<DomainAcknowledgement> acknowledgements)
    {
        string canonical = string.Join(
            '\n',
            acknowledgements.OrderBy(item => item.AcknowledgementId, StringComparer.Ordinal)
                .ThenBy(item => item.AcknowledgementVersion)
                .Select(item => $"{item.AcknowledgementId}:{item.AcknowledgementVersion}"));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
