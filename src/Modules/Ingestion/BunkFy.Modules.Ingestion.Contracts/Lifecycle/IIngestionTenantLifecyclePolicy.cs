namespace BunkFy.Modules.Ingestion.Contracts;

public interface IIngestionTenantLifecyclePolicy
{
    ValueTask<IngestionTenantLifecycleDecision> AuthorizeAsync(
        string tenantId,
        IngestionTenantLifecycleOperation operation,
        CancellationToken cancellationToken = default);
}

public sealed record IngestionTenantLifecycleDecision(
    IngestionTenantLifecycleOutcome Outcome)
{
    public static IngestionTenantLifecycleDecision Allowed { get; } =
        new(IngestionTenantLifecycleOutcome.Allowed);

    public static IngestionTenantLifecycleDecision Restricted { get; } =
        new(IngestionTenantLifecycleOutcome.Restricted);

    public static IngestionTenantLifecycleDecision Unavailable { get; } =
        new(IngestionTenantLifecycleOutcome.Unavailable);
}

public enum IngestionTenantLifecycleOperation
{
    Unknown = 0,
    ConnectionProvisioning = 1,
    AdapterRunStart = 2,
    AdapterIngress = 3
}

public enum IngestionTenantLifecycleOutcome
{
    Unknown = 0,
    Allowed = 1,
    Restricted = 2,
    Unavailable = 3
}
