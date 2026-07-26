namespace BunkFy.Modules.DataRights.Application;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.ValueObjects;

internal static class DataRightsApprovalEvidenceComparer
{
    public static bool Matches(
        DataRightsApprovalPolicyEvidence frozen,
        DataRightsApprovalEvidence current) =>
        frozen.SchemaVersion == current.SchemaVersion &&
        frozen.PropertyId == current.PropertyId &&
        frozen.PropertyVersion == current.PropertyVersion &&
        string.Equals(
            frozen.OperatingCountryCode,
            current.OperatingCountryCode,
            StringComparison.Ordinal) &&
        string.Equals(frozen.PolicyId, current.PolicyId, StringComparison.Ordinal) &&
        frozen.PolicyVersion == current.PolicyVersion &&
        string.Equals(
            frozen.RetentionPolicyId,
            current.RetentionPolicyId,
            StringComparison.Ordinal) &&
        frozen.RetentionPolicyVersion == current.RetentionPolicyVersion &&
        string.Equals(
            frozen.ContentSha256,
            current.ContentSha256,
            StringComparison.Ordinal) &&
        string.Equals(frozen.PurposeCode, current.PurposeCode, StringComparison.Ordinal) &&
        string.Equals(frozen.Surface, current.Surface, StringComparison.Ordinal) &&
        string.Equals(
            frozen.SourceProvenance,
            current.SourceProvenance,
            StringComparison.Ordinal) &&
        frozen.EvaluatedAtUtc == current.EvaluatedAtUtc &&
        frozen.RequiresDistinctExecutor == current.RequiresDistinctExecutor;
}
