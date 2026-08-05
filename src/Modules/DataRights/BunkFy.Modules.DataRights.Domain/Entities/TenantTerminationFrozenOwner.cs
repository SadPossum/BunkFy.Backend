namespace BunkFy.Modules.DataRights.Domain.Entities;

using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Errors;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Results;

public sealed class TenantTerminationFrozenOwner
{
    public const int OwnerKeyMaxLength = 100;

    private TenantTerminationFrozenOwner() { }

    private TenantTerminationFrozenOwner(
        int ordinal,
        string ownerKey,
        int contractVersion,
        int catalogVersion,
        string catalogSha256)
    {
        this.Ordinal = ordinal;
        this.OwnerKey = ownerKey;
        this.ContractVersion = contractVersion;
        this.CatalogVersion = catalogVersion;
        this.CatalogSha256 = catalogSha256;
    }

    public int Ordinal { get; private set; }
    public string OwnerKey { get; private set; } = string.Empty;
    public int ContractVersion { get; private set; }
    public int CatalogVersion { get; private set; }
    public string CatalogSha256 { get; private set; } = string.Empty;

    internal static Result<TenantTerminationFrozenOwner> Create(
        int ordinal,
        TenantTerminationFrozenOwnerDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        string ownerKey = descriptor.OwnerKey?.Trim() ?? string.Empty;
        if (ordinal is <= 0 or >
                TenantTerminationProcess.MaximumFrozenOwners ||
            !IsStableOwnerKey(ownerKey) ||
            descriptor.ContractVersion <= 0 ||
            descriptor.CatalogVersion <= 0 ||
            !TenantTerminationProcess.IsSha256(descriptor.CatalogSha256))
        {
            return Result.Failure<TenantTerminationFrozenOwner>(
                DataRightsDomainErrors.TenantTerminationFreezeCheckpointInvalid);
        }

        return Result.Success(new TenantTerminationFrozenOwner(
            ordinal,
            ownerKey,
            descriptor.ContractVersion,
            descriptor.CatalogVersion,
            descriptor.CatalogSha256));
    }

    internal bool Matches(TenantTerminationFrozenOwnerDescriptor descriptor) =>
        string.Equals(
            this.OwnerKey,
            descriptor.OwnerKey?.Trim(),
            StringComparison.Ordinal) &&
        this.ContractVersion == descriptor.ContractVersion &&
        this.CatalogVersion == descriptor.CatalogVersion &&
        string.Equals(
            this.CatalogSha256,
            descriptor.CatalogSha256,
            StringComparison.Ordinal);

    private static bool IsStableOwnerKey(string value) =>
        value.Length is > 0 and <= OwnerKeyMaxLength &&
        value[0] is >= 'a' and <= 'z' &&
        value.All(character =>
            character is (>= 'a' and <= 'z') or
                (>= '0' and <= '9') or '.' or '-' or '_');
}
