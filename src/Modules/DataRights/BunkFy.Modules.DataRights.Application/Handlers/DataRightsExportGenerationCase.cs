namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;

internal static class DataRightsExportGenerationCase
{
    public static bool Matches(
        DataRightsCase dataRightsCase,
        DataRightsExportArtifact artifact,
        DataRightsCaseScope scope,
        Guid caseId,
        long decisionRevision,
        out DataRights.Contracts.DataRightsSubjectCoordinate[] subjects)
    {
        subjects = RequestDataRightsExportCommandHandler.ToCoordinates(dataRightsCase);
        return dataRightsCase.Id == caseId &&
            artifact.CaseId == caseId &&
            artifact.CaseKind == (DataRightsCaseKind)scope.CaseType &&
            artifact.PropertyId == scope.PropertyId &&
            artifact.DecisionRevision == decisionRevision &&
            dataRightsCase.DecisionRevision == decisionRevision &&
            dataRightsCase.Kind == artifact.CaseKind &&
            dataRightsCase.PropertyId == artifact.PropertyId &&
            dataRightsCase.Status == DataRightsCaseState.Approved &&
            dataRightsCase.Decision == DataRightsCaseDecision.Approved &&
            dataRightsCase.DecisionReason ==
                DataRightsCaseDecisionReason.RequestValidated &&
            dataRightsCase.RequestedOperations ==
                DataRightsCaseOperation.AccessExport &&
            subjects.Length == artifact.SelectedSubjectCount &&
            string.Equals(
                DataRightsExportIdentity.SelectionSha256(subjects),
                artifact.SelectionSha256,
                StringComparison.Ordinal);
    }
}
