namespace BunkFy.Modules.DataRights.Domain.ValueObjects;

using BunkFy.Modules.DataRights.Domain.Errors;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Results;

public sealed record DataRightsExecutionScope
{
    public static DataRightsExecutionScope Staff { get; } =
        new(
            DataRightsCaseKind.StaffRights,
            DataRightsCaseScopeKind.Tenant,
            propertyId: null);

    public static DataRightsExecutionScope TenantTermination { get; } =
        new(
            DataRightsCaseKind.TenantTermination,
            DataRightsCaseScopeKind.Tenant,
            propertyId: null);

    private DataRightsExecutionScope(
        DataRightsCaseKind caseKind,
        DataRightsCaseScopeKind scopeKind,
        Guid? propertyId)
    {
        this.CaseKind = caseKind;
        this.ScopeKind = scopeKind;
        this.PropertyId = propertyId;
    }

    public DataRightsCaseKind CaseKind { get; }

    public DataRightsCaseScopeKind ScopeKind { get; }

    public Guid? PropertyId { get; }

    public static DataRightsExecutionScope ForProperty(Guid propertyId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(propertyId, Guid.Empty);
        return new(
            DataRightsCaseKind.GuestRights,
            DataRightsCaseScopeKind.Property,
            propertyId);
    }

    public static Result<DataRightsExecutionScope> Create(
        DataRightsCaseKind caseKind,
        DataRightsCaseScopeKind scopeKind,
        Guid? propertyId)
    {
        bool valid = scopeKind switch
        {
            DataRightsCaseScopeKind.Property =>
                caseKind == DataRightsCaseKind.GuestRights &&
                propertyId is Guid value &&
                value != Guid.Empty,
            DataRightsCaseScopeKind.Tenant =>
                caseKind is DataRightsCaseKind.StaffRights or
                    DataRightsCaseKind.TenantTermination &&
                propertyId is null,
            _ => false
        };
        return valid
            ? Result.Success(
                new DataRightsExecutionScope(
                    caseKind,
                    scopeKind,
                    propertyId))
            : Result.Failure<DataRightsExecutionScope>(
                DataRightsDomainErrors.ExecutionCoordinateInvalid);
    }

    public bool Matches(
        DataRightsCaseKind caseKind,
        DataRightsCaseScopeKind scopeKind,
        Guid? propertyId) =>
        this.CaseKind == caseKind &&
        this.ScopeKind == scopeKind &&
        this.PropertyId == propertyId;
}
