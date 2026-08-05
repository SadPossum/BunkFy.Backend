namespace BunkFy.Modules.DataRights.Contracts;

public static class TenantTerminationCoordination
{
    public const string ExecutorActorId = "system:tenant-termination";
    public const int MaximumTaskAttempts = 5;

    private const string TenantMutationResourcePrefix =
        "bunkfy:tenant-termination:mutation:";

    public static string CreateTenantMutationResource(
        string canonicalTenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalTenantId);
        return TenantMutationResourcePrefix + canonicalTenantId;
    }
}
