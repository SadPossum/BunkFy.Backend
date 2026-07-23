namespace BunkFy.Modules.Properties.Domain.Aggregates;

using BunkFy.Modules.Properties.Domain.Errors;
using BunkFy.Modules.Properties.Domain.Events;
using BunkFy.Modules.Properties.Domain.ValueObjects;
using Gma.Framework.Results;

public sealed partial class Property
{
    private readonly List<PropertyGovernanceAcknowledgement> governanceAcknowledgements = [];

    public IReadOnlyCollection<PropertyGovernanceAcknowledgement> GovernanceAcknowledgements =>
        this.governanceAcknowledgements.AsReadOnly();

    public Result ActivateProcessing(
        PropertyGovernanceBinding binding,
        IReadOnlyCollection<PropertyGovernanceAcknowledgement> acknowledgements,
        long expectedVersion,
        Guid eventId,
        DateTimeOffset nowUtc,
        string actorId)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(acknowledgements);

        Result statusResult = this.EnsureActive();
        if (statusResult.IsFailure)
        {
            return statusResult;
        }

        Result versionResult = this.EnsureExpectedVersion(expectedVersion);
        if (versionResult.IsFailure)
        {
            return versionResult;
        }

        if (eventId == Guid.Empty)
        {
            return Result.Failure(PropertiesDomainErrors.DomainEventIdRequired);
        }

        if (!TryNormalizeRequiredActor(actorId, out string normalizedActorId))
        {
            return Result.Failure(PropertiesDomainErrors.ActorIdInvalid);
        }

        if (acknowledgements.Count > PropertyGovernanceAcknowledgement.MaximumAcknowledgements ||
            acknowledgements.Distinct().Count() != acknowledgements.Count)
        {
            return Result.Failure(PropertiesDomainErrors.PolicyAcknowledgementsInvalid);
        }

        this.GovernanceBinding = binding;
        this.governanceAcknowledgements.Clear();
        this.governanceAcknowledgements.AddRange(acknowledgements.OrderBy(
            acknowledgement => acknowledgement.AcknowledgementId,
            StringComparer.Ordinal).ThenBy(acknowledgement => acknowledgement.AcknowledgementVersion));
        this.ProcessingState = PropertyProcessingState.Enabled;
        this.UpdatedAtUtc = nowUtc;
        this.Version++;

        this.RaiseDomainEvent(new PropertyProcessingPolicyActivatedDomainEvent(
            eventId,
            nowUtc,
            this.Id,
            this.ScopeId,
            binding,
            [.. this.governanceAcknowledgements],
            normalizedActorId,
            this.Version));

        return Result.Success();
    }

    public Result SuspendProcessing(
        long expectedVersion,
        Guid eventId,
        DateTimeOffset nowUtc,
        string actorId)
    {
        Result statusResult = this.EnsureActive();
        if (statusResult.IsFailure)
        {
            return statusResult;
        }

        Result versionResult = this.EnsureExpectedVersion(expectedVersion);
        if (versionResult.IsFailure)
        {
            return versionResult;
        }

        if (this.ProcessingState != PropertyProcessingState.Enabled || this.GovernanceBinding is null)
        {
            return Result.Failure(PropertiesDomainErrors.PropertyProcessingNotEnabled);
        }

        if (eventId == Guid.Empty)
        {
            return Result.Failure(PropertiesDomainErrors.DomainEventIdRequired);
        }

        if (!TryNormalizeRequiredActor(actorId, out string normalizedActorId))
        {
            return Result.Failure(PropertiesDomainErrors.ActorIdInvalid);
        }

        this.ProcessingState = PropertyProcessingState.Suspended;
        this.UpdatedAtUtc = nowUtc;
        this.Version++;

        this.RaiseDomainEvent(new PropertyProcessingSuspendedDomainEvent(
            eventId,
            nowUtc,
            this.Id,
            this.ScopeId,
            this.GovernanceBinding,
            [.. this.governanceAcknowledgements],
            normalizedActorId,
            this.Version));

        return Result.Success();
    }

    private static bool TryNormalizeRequiredActor(string? value, out string normalized)
    {
        normalized = value?.Trim() ?? string.Empty;
        return normalized.Length is > 0 and <= ActorIdMaxLength && !normalized.Any(char.IsControl);
    }
}
