namespace BunkFy.Modules.Ingestion.Application.DataRights;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Ingestion.Application.Policies;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Ingestion.Domain.DataRights;
using Gma.Framework.Naming;

internal static class
    IngestionAnonymisationExecutionRequestValidator
{
    public static bool IsValid(
        DataRightsAnonymisationContributionRequest? request,
        string tenantId,
        DateTimeOffset nowUtc)
    {
        if (request is null ||
            !TenantIds.TryNormalize(
                tenantId,
                out string? normalizedTenant) ||
            !TenantIds.TryNormalize(
                request.TenantId,
                out string? requestTenant) ||
            !string.Equals(
                normalizedTenant,
                requestTenant,
                StringComparison.Ordinal) ||
            request.ContractVersion !=
                DataRightsAnonymisationContract.CurrentVersion ||
            request.Coordinate is null ||
            request.RoutingPolicy is null ||
            !string.Equals(
                request.Coordinate.OwnerKey,
                IngestionDataRightsCoordinates.Owner,
                StringComparison.Ordinal) ||
            !string.Equals(
                request.Coordinate.RecordType,
                IngestionDataRightsCoordinates
                    .ReservationSourceLinkRecordType,
                StringComparison.Ordinal) ||
            request.WorkItemId == Guid.Empty ||
            request.IdempotencyKey == Guid.Empty ||
            request.RoutingPropertyId == Guid.Empty ||
            request.CaseId == Guid.Empty ||
            request.ApprovalRevision <= 0 ||
            request.OperationRevision <=
                request.ApprovalRevision ||
            request.Coordinate.RecordId == Guid.Empty ||
            request.Coordinate.RecordVersion <= 0 ||
            request.RoutingPolicy.PropertyId !=
                request.RoutingPropertyId ||
            request.DeadlineUtc <= nowUtc)
        {
            return false;
        }

        string actor = request.ExecutingActorId?.Trim() ??
            string.Empty;
        if (actor.Length is 0 or >
            IngestionAnonymisationReceipt.ActorIdMaxLength)
        {
            return false;
        }

        IngestionAnonymisationRoutingPolicyEvidence policy =
            IngestionAnonymisationPolicyEvidence.FromApproval(
                request.RoutingPolicy);
        return IngestionAnonymisationPolicyEvidence.IsValid(policy);
    }
}
