namespace BunkFy.Modules.DataRights.Application.Models;

using BunkFy.Modules.DataRights.Contracts;

public sealed record DataRightsCaseScope
{
    private DataRightsCaseScope(
        DataRightsCaseType caseType,
        Guid? propertyId)
    {
        this.CaseType = caseType;
        this.PropertyId = propertyId;
    }

    public static DataRightsCaseScope Staff { get; } =
        new(DataRightsCaseType.StaffRights, propertyId: null);

    public static DataRightsCaseScope TenantTermination { get; } =
        new(DataRightsCaseType.TenantTermination, propertyId: null);

    public DataRightsCaseType CaseType { get; }

    public Guid? PropertyId { get; }

    public bool IsTenant => !this.PropertyId.HasValue;

    public static DataRightsCaseScope ForProperty(Guid propertyId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(propertyId, Guid.Empty);
        return new DataRightsCaseScope(DataRightsCaseType.GuestRights, propertyId);
    }
}
