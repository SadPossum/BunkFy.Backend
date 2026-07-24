namespace BunkFy.Modules.DataRights.Application.Authorization;

using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Contracts.Authorization;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Naming;
using SelectedSubject = BunkFy.Modules.DataRights.Domain.Entities.DataRightsSubjectCoordinate;

internal sealed class DataRightsOperationApprovalGate(IDataRightsCaseRepository cases)
    : IDataRightsOperationApprovalGate
{
    public async Task<DataRightsOperationApprovalResult> EvaluateAsync(
        DataRightsOperationApprovalRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!TryValidate(request, out string? tenantId, out string? ownerKey, out string? recordType))
        {
            return DataRightsOperationApprovalResult.Denied(
                DataRightsOperationApprovalDenial.InvalidRequest);
        }

        DataRightsCase? dataRightsCase = await cases.GetAsync(
            request.PropertyId,
            request.CaseId,
            cancellationToken).ConfigureAwait(false);
        if (dataRightsCase is null ||
            !string.Equals(dataRightsCase.ScopeId, tenantId, StringComparison.Ordinal))
        {
            return DataRightsOperationApprovalResult.Denied(
                DataRightsOperationApprovalDenial.CaseNotFound);
        }

        if (dataRightsCase.Decision != DataRightsCaseDecision.Approved ||
            dataRightsCase.Status is not DataRightsCaseState.Approved
                and not DataRightsCaseState.Executing)
        {
            return DataRightsOperationApprovalResult.Denied(
                DataRightsOperationApprovalDenial.CaseNotApproved);
        }

        if (dataRightsCase.DecisionRevision != request.ApprovalRevision)
        {
            return DataRightsOperationApprovalResult.Denied(
                DataRightsOperationApprovalDenial.ApprovalRevisionMismatch);
        }

        if ((((DataRightsOperation)dataRightsCase.RequestedOperations) & request.Operation) !=
            request.Operation)
        {
            return DataRightsOperationApprovalResult.Denied(
                DataRightsOperationApprovalDenial.OperationNotApproved);
        }

        bool subjectApproved = dataRightsCase.SelectedSubjects.Any(subject =>
            string.Equals(subject.OwnerKey, ownerKey, StringComparison.Ordinal) &&
            string.Equals(subject.RecordType, recordType, StringComparison.Ordinal) &&
            subject.RecordId == request.RecordId &&
            subject.RecordVersion == request.RecordVersion);
        return subjectApproved
            ? DataRightsOperationApprovalResult.Approved
            : DataRightsOperationApprovalResult.Denied(
                DataRightsOperationApprovalDenial.SubjectNotApproved);
    }

    private static bool TryValidate(
        DataRightsOperationApprovalRequest request,
        out string? tenantId,
        out string? ownerKey,
        out string? recordType)
    {
        bool tenantValid = TenantIds.TryNormalize(request.TenantId, out tenantId);
        ownerKey = request.OwnerKey?.Trim().ToLowerInvariant();
        recordType = request.RecordType?.Trim().ToLowerInvariant();
        int operation = (int)request.Operation;
        return tenantValid &&
            request.PropertyId != Guid.Empty &&
            request.CaseId != Guid.Empty &&
            request.ApprovalRevision > 0 &&
            operation is > 0 and <= (int)DataRightsOperation.Anonymisation &&
            (operation & (operation - 1)) == 0 &&
            ownerKey is not null &&
            ownerKey.Length is > 0 and <= SelectedSubject.OwnerKeyMaxLength &&
            recordType is not null &&
            recordType.Length is > 0 and <= SelectedSubject.RecordTypeMaxLength &&
            request.RecordId != Guid.Empty &&
            request.RecordVersion > 0;
    }
}
