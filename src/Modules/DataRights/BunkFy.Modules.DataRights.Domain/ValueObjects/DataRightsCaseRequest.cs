namespace BunkFy.Modules.DataRights.Domain.ValueObjects;

using BunkFy.Modules.DataRights.Domain.Errors;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Results;

public sealed class DataRightsCaseRequest
{
    private const DataRightsCaseOperation KnownOperations =
        DataRightsCaseOperation.AccessExport |
        DataRightsCaseOperation.Correction |
        DataRightsCaseOperation.Restriction |
        DataRightsCaseOperation.Erasure |
        DataRightsCaseOperation.Anonymisation;

    private DataRightsCaseRequest(
        Guid? propertyId,
        DataRightsCaseKind kind,
        DataRightsCaseOperation requestedOperations,
        DataRightsRequesterRelation requesterRelationship)
    {
        this.PropertyId = propertyId;
        this.Kind = kind;
        this.RequestedOperations = requestedOperations;
        this.RequesterRelationship = requesterRelationship;
    }

    public Guid? PropertyId { get; }
    public DataRightsCaseKind Kind { get; }
    public DataRightsCaseOperation RequestedOperations { get; }
    public DataRightsRequesterRelation RequesterRelationship { get; }

    public static Result<DataRightsCaseRequest> Create(
        Guid? propertyId,
        DataRightsCaseKind kind,
        DataRightsCaseOperation requestedOperations,
        DataRightsRequesterRelation requesterRelationship)
    {
        if (kind is not DataRightsCaseKind.GuestRights and not DataRightsCaseKind.TenantTermination)
        {
            return Result.Failure<DataRightsCaseRequest>(DataRightsDomainErrors.CaseTypeInvalid);
        }

        if (requestedOperations == DataRightsCaseOperation.None ||
            (requestedOperations & ~KnownOperations) != DataRightsCaseOperation.None)
        {
            return Result.Failure<DataRightsCaseRequest>(DataRightsDomainErrors.OperationsInvalid);
        }

        if (requesterRelationship is not DataRightsRequesterRelation.DataSubject
            and not DataRightsRequesterRelation.AuthorizedRepresentative
            and not DataRightsRequesterRelation.ControllerInitiated
            and not DataRightsRequesterRelation.TenantOwner)
        {
            return Result.Failure<DataRightsCaseRequest>(
                DataRightsDomainErrors.RequesterRelationshipInvalid);
        }

        if (kind == DataRightsCaseKind.GuestRights &&
            (!propertyId.HasValue || propertyId.Value == Guid.Empty))
        {
            return Result.Failure<DataRightsCaseRequest>(DataRightsDomainErrors.PropertyRequired);
        }

        if (kind == DataRightsCaseKind.GuestRights &&
            requesterRelationship == DataRightsRequesterRelation.TenantOwner)
        {
            return Result.Failure<DataRightsCaseRequest>(
                DataRightsDomainErrors.GuestRightsRequesterInvalid);
        }

        if (kind == DataRightsCaseKind.TenantTermination && propertyId.HasValue)
        {
            return Result.Failure<DataRightsCaseRequest>(DataRightsDomainErrors.PropertyNotAllowed);
        }

        if (kind == DataRightsCaseKind.TenantTermination &&
            requesterRelationship is not DataRightsRequesterRelation.ControllerInitiated
                and not DataRightsRequesterRelation.TenantOwner)
        {
            return Result.Failure<DataRightsCaseRequest>(
                DataRightsDomainErrors.TenantTerminationRequesterInvalid);
        }

        return Result.Success(new DataRightsCaseRequest(
            propertyId,
            kind,
            requestedOperations,
            requesterRelationship));
    }
}
