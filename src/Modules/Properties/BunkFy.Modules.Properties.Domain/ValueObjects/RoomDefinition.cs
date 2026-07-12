namespace BunkFy.Modules.Properties.Domain.ValueObjects;

using Gma.Framework.Naming;
using Gma.Framework.Results;
using BunkFy.Modules.Properties.Domain.Errors;

public sealed record RoomDefinition(
    string ScopeId,
    RoomName Name,
    PhysicalLabel? BuildingLabel,
    PhysicalLabel? FloorLabel)
{
    public static Result<RoomDefinition> Create(
        string? tenantId,
        string? name,
        string? buildingLabel,
        string? floorLabel)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Result.Failure<RoomDefinition>(PropertiesDomainErrors.TenantRequired);
        }

        if (!TenantIds.TryNormalize(tenantId, out string? normalizedTenantId))
        {
            return Result.Failure<RoomDefinition>(PropertiesDomainErrors.TenantInvalid);
        }

        Result<RoomName> nameResult = RoomName.Create(name);
        if (nameResult.IsFailure)
        {
            return Result.Failure<RoomDefinition>(nameResult.Error);
        }

        if (!TryCreateOptionalLabel(buildingLabel, out PhysicalLabel? building, out Error? buildingError))
        {
            return Result.Failure<RoomDefinition>(buildingError!);
        }

        if (!TryCreateOptionalLabel(floorLabel, out PhysicalLabel? floor, out Error? floorError))
        {
            return Result.Failure<RoomDefinition>(floorError!);
        }

        return Result.Success(new RoomDefinition(
            normalizedTenantId,
            nameResult.Value,
            building,
            floor));
    }

    private static bool TryCreateOptionalLabel(string? value, out PhysicalLabel? label, out Error? error)
    {
        label = null;
        error = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        Result<PhysicalLabel> result = PhysicalLabel.Create(value);
        if (result.IsFailure)
        {
            error = result.Error;
            return false;
        }

        label = result.Value;
        return true;
    }
}
