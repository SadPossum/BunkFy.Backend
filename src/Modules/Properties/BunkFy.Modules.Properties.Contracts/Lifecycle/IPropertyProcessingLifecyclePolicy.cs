namespace BunkFy.Modules.Properties.Contracts;

public interface IPropertyProcessingLifecyclePolicy
{
    ValueTask<PropertyProcessingLifecycleDecision> AuthorizeActivationAsync(
        string tenantId,
        Guid propertyId,
        CancellationToken cancellationToken = default);
}

public sealed record PropertyProcessingLifecycleDecision(
    PropertyProcessingLifecycleOutcome Outcome)
{
    public static PropertyProcessingLifecycleDecision Allowed { get; } =
        new(PropertyProcessingLifecycleOutcome.Allowed);

    public static PropertyProcessingLifecycleDecision Restricted { get; } =
        new(PropertyProcessingLifecycleOutcome.Restricted);

    public static PropertyProcessingLifecycleDecision Unavailable { get; } =
        new(PropertyProcessingLifecycleOutcome.Unavailable);
}

public enum PropertyProcessingLifecycleOutcome
{
    Unknown = 0,
    Allowed = 1,
    Restricted = 2,
    Unavailable = 3
}
