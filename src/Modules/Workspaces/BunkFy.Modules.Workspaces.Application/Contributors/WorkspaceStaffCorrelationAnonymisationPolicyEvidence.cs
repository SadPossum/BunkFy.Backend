namespace BunkFy.Modules.Workspaces.Application.Contributors;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Contracts;

internal static class
    WorkspaceStaffCorrelationAnonymisationPolicyEvidence
{
    public static bool IsValid(
        DataRightsApprovalEvidence? evidence) =>
        evidence is not null &&
        evidence.SchemaVersion == 2 &&
        evidence.CaseType == DataRightsCaseType.StaffRights &&
        evidence.ScopeKind == DataRightsExecutionScopeKind.Tenant &&
        evidence.PropertyId is null &&
        evidence.PropertyVersion == 0 &&
        evidence.RequiresDistinctExecutor &&
        !string.IsNullOrWhiteSpace(evidence.RetentionDataClass) &&
        !string.IsNullOrWhiteSpace(evidence.RetentionTrigger) &&
        evidence.RetentionTriggeredAtUtc.HasValue &&
        evidence.RetentionDeadlineUtc.HasValue &&
        evidence.RetentionDeadlineUtc <= evidence.EvaluatedAtUtc &&
        HasValidBindings(evidence, minimumCount: 5) &&
        IsSha256(evidence.StateBindingsSha256) &&
        IsSha256(evidence.ContentSha256) &&
        TryGetBinding(evidence, out _);

    public static bool MatchesFrozenBinding(
        DataRightsApprovalEvidence evidence,
        WorkspaceStaffCorrelationAnonymisationSnapshot snapshot)
    {
        if (!IsValid(evidence) ||
            snapshot.Status !=
                WorkspaceStaffCorrelationAnonymisationSnapshotStatus
                    .Eligible ||
            snapshot.AnchorProcessVersion is null ||
            !IsSha256(snapshot.StateSha256) ||
            !TryGetBinding(
                evidence,
                out DataRightsApprovalEvidenceBinding? binding))
        {
            return false;
        }

        return binding!.Version ==
                snapshot.AnchorProcessVersion.Value &&
            string.Equals(
                binding.Sha256,
                snapshot.StateSha256,
                StringComparison.Ordinal);
    }

    public static string ComputeApprovalSha256(
        DataRightsApprovalEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        List<string> values =
        [
            evidence.SchemaVersion.ToString(
                CultureInfo.InvariantCulture),
            ((int)evidence.CaseType).ToString(
                CultureInfo.InvariantCulture),
            ((int)evidence.ScopeKind).ToString(
                CultureInfo.InvariantCulture),
            evidence.PropertyId?.ToString("N") ?? "-",
            evidence.PropertyVersion.ToString(
                CultureInfo.InvariantCulture),
            evidence.OperatingCountryCode,
            evidence.PolicyId,
            evidence.PolicyVersion.ToString(
                CultureInfo.InvariantCulture),
            evidence.RetentionPolicyId,
            evidence.RetentionPolicyVersion.ToString(
                CultureInfo.InvariantCulture),
            evidence.ContentSha256,
            evidence.PurposeCode,
            evidence.Surface,
            evidence.SourceProvenance,
            evidence.RetentionDataClass ?? "-",
            evidence.RetentionTrigger ?? "-",
            Coordinate(evidence.RetentionTriggeredAtUtc),
            Coordinate(evidence.RetentionDeadlineUtc),
            Coordinate(evidence.EvaluatedAtUtc),
            evidence.RequiresDistinctExecutor ? "1" : "0",
            evidence.StateBindingsSha256 ?? "-"
        ];
        foreach (DataRightsApprovalEvidenceBinding binding in
            (evidence.StateBindings ?? []).OrderBy(
                binding => binding.Key,
                StringComparer.Ordinal))
        {
            values.Add(binding.Key);
            values.Add(binding.Version.ToString(
                CultureInfo.InvariantCulture));
            values.Add(binding.Sha256);
        }

        return Compute(values);
    }

    public static string? GetFrozenBindingSha256(
        DataRightsApprovalEvidence evidence) =>
        TryGetBinding(
            evidence,
            out DataRightsApprovalEvidenceBinding? binding)
            ? binding!.Sha256
            : null;

    private static bool TryGetBinding(
        DataRightsApprovalEvidence evidence,
        out DataRightsApprovalEvidenceBinding? binding)
    {
        DataRightsApprovalEvidenceBinding[] matches =
            (evidence.StateBindings ?? [])
            .Where(candidate =>
                candidate is not null &&
                string.Equals(
                    candidate.Key,
                    WorkspacesDataRightsCoordinates
                        .StaffCorrelationStateBindingKey,
                    StringComparison.Ordinal))
            .Take(2)
            .ToArray();
        binding = matches.Length == 1 ? matches[0] : null;
        return binding is not null &&
            binding.Version > 0 &&
            IsSha256(binding.Sha256);
    }

    private static bool HasValidBindings(
        DataRightsApprovalEvidence evidence,
        int minimumCount)
    {
        IReadOnlyCollection<DataRightsApprovalEvidenceBinding>?
            bindings = evidence.StateBindings;
        if (bindings is null ||
            bindings.Count < minimumCount ||
            bindings.Count >
                DataRightsAnonymisationPolicyContract
                    .MaximumStateBindings)
        {
            return false;
        }

        HashSet<string> keys = new(StringComparer.Ordinal);
        foreach (DataRightsApprovalEvidenceBinding binding in bindings)
        {
            if (binding is null ||
                !IsBindingKey(binding.Key) ||
                binding.Version < 0 ||
                !IsSha256(binding.Sha256) ||
                !keys.Add(binding.Key))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsBindingKey(string? value)
    {
        string key = value ?? string.Empty;
        return key.Length is > 0 and <=
                DataRightsAnonymisationPolicyContract.KeyMaxLength &&
            key[0] is >= 'a' and <= 'z' &&
            key.All(character =>
                character is (>= 'a' and <= 'z') or
                    (>= '0' and <= '9') or '.' or '-' or '_');
    }

    private static string Compute(IEnumerable<string> values)
    {
        using IncrementalHash hash =
            IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string value in values)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{value.Length}:{value}"));
            try
            {
                hash.AppendData(bytes);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(bytes);
            }
        }

        byte[] digest = hash.GetHashAndReset();
        try
        {
            return Convert.ToHexStringLower(digest);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(digest);
        }
    }

    private static string Coordinate(DateTimeOffset? value) =>
        value?.ToUniversalTime().ToString(
            "O",
            CultureInfo.InvariantCulture) ?? "-";

    private static bool IsSha256(string? value) =>
        value is
        {
            Length:
                DataRightsAnonymisationPolicyContract.Sha256Length
        } &&
        value.All(character =>
            character is (>= '0' and <= '9') or
                (>= 'a' and <= 'f'));
}
