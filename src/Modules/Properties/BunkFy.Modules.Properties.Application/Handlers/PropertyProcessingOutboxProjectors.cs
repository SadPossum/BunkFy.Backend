namespace BunkFy.Modules.Properties.Application.Handlers;

using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Properties.Domain.Events;
using Gma.Framework.Application.Events;
using Gma.Framework.Messaging;

internal sealed class PropertyProcessingPolicyActivatedOutboxProjector(IOutboxWriterRegistry outboxWriters)
    : IDomainEventHandler<PropertyProcessingPolicyActivatedDomainEvent>
{
    public Task HandleAsync(
        PropertyProcessingPolicyActivatedDomainEvent domainEvent,
        CancellationToken cancellationToken) =>
        outboxWriters.GetRequired(PropertiesModuleMetadata.Name).EnqueueAsync(
            new PropertyProcessingPolicyActivatedIntegrationEvent(
                domainEvent.EventId,
                domainEvent.ScopeId,
                domainEvent.OccurredAtUtc,
                domainEvent.PropertyId,
                new PropertyGovernancePolicyBinding(
                    domainEvent.Binding.OperatingCountryCode,
                    domainEvent.Binding.PolicyId,
                    domainEvent.Binding.PolicyVersion,
                    domainEvent.Binding.DataRegionId,
                    domainEvent.Binding.TransferProfileId,
                    domainEvent.Binding.RetentionPolicyId,
                    domainEvent.Binding.RetentionPolicyVersion,
                    domainEvent.Binding.ContentSha256,
                    domainEvent.Binding.PolicyEffectiveAtUtc,
                    domainEvent.Binding.PolicyExpiresAtUtc,
                    domainEvent.Binding.ActivatedAtUtc,
                    domainEvent.Acknowledgements.Select(acknowledgement =>
                        new PropertyGovernanceAcknowledgement(
                            acknowledgement.AcknowledgementId,
                            acknowledgement.AcknowledgementVersion)).ToArray()),
                domainEvent.PropertyVersion),
            cancellationToken);
}

internal sealed class PropertyProcessingSuspendedOutboxProjector(IOutboxWriterRegistry outboxWriters)
    : IDomainEventHandler<PropertyProcessingSuspendedDomainEvent>
{
    public Task HandleAsync(
        PropertyProcessingSuspendedDomainEvent domainEvent,
        CancellationToken cancellationToken) =>
        outboxWriters.GetRequired(PropertiesModuleMetadata.Name).EnqueueAsync(
            new PropertyProcessingSuspendedIntegrationEvent(
                domainEvent.EventId,
                domainEvent.ScopeId,
                domainEvent.OccurredAtUtc,
                domainEvent.PropertyId,
                MapBinding(domainEvent.Binding, domainEvent.Acknowledgements),
                domainEvent.PropertyVersion),
            cancellationToken);

    private static PropertyGovernancePolicyBinding MapBinding(
        Domain.ValueObjects.PropertyGovernanceBinding binding,
        IReadOnlyCollection<Domain.ValueObjects.PropertyGovernanceAcknowledgement> acknowledgements) =>
        new(
            binding.OperatingCountryCode,
            binding.PolicyId,
            binding.PolicyVersion,
            binding.DataRegionId,
            binding.TransferProfileId,
            binding.RetentionPolicyId,
            binding.RetentionPolicyVersion,
            binding.ContentSha256,
            binding.PolicyEffectiveAtUtc,
            binding.PolicyExpiresAtUtc,
            binding.ActivatedAtUtc,
            acknowledgements.Select(acknowledgement =>
                new PropertyGovernanceAcknowledgement(
                    acknowledgement.AcknowledgementId,
                    acknowledgement.AcknowledgementVersion)).ToArray());
}
