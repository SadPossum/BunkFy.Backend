namespace BunkFy.Modules.Staff.Application.Contributors;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Domain.DataRights;
using BunkFy.Modules.Staff.Domain.Entities;
using BunkFy.Modules.Staff.Domain.Governance;

internal static class StaffAnonymisationPolicyEvidence
{
    private static readonly string[] RequiredBindingKeys =
    [
        "staff.governance",
        "staff.holds",
        "staff.record",
        "staff.restriction"
    ];

    public static IReadOnlyCollection<DataRightsApprovalEvidenceBinding>
        CreateBindings(
            StaffMember member,
            StaffEmploymentGovernance governance,
            StaffProcessingRestrictionProjection restriction,
            IReadOnlyCollection<StaffDataHold> holds,
            long operationLockRevision) =>
        [
            new(
                "staff.record",
                member.Version,
                ComputeRecordDigest(member)),
            new(
                "staff.governance",
                governance.Version,
                ComputeGovernanceDigest(governance)),
            new(
                "staff.restriction",
                restriction.Revision,
                ComputeRestrictionDigest(restriction)),
            new(
                "staff.holds",
                operationLockRevision,
                ComputeHoldDigest(holds))
        ];

    public static bool MatchesFrozenBindings(
        DataRightsApprovalEvidence evidence,
        StaffMember member,
        StaffEmploymentGovernance governance,
        StaffProcessingRestrictionProjection restriction,
        IReadOnlyCollection<StaffDataHold> holds,
        long selectedOperationLockRevision)
    {
        if (!HasValidBindings(evidence, minimumCount: 4) ||
            selectedOperationLockRevision < 1)
        {
            return false;
        }

        DataRightsApprovalEvidenceBinding[] expected =
            CreateBindings(
                member,
                governance,
                restriction,
                holds,
                selectedOperationLockRevision)
            .OrderBy(binding => binding.Key, StringComparer.Ordinal)
            .ToArray();
        IReadOnlyCollection<DataRightsApprovalEvidenceBinding>
            stateBindings = evidence.StateBindings!;
        DataRightsApprovalEvidenceBinding[] frozen =
            stateBindings
                .Where(binding =>
                    RequiredBindingKeys.Contains(
                        binding.Key,
                        StringComparer.Ordinal))
                .OrderBy(binding => binding.Key, StringComparer.Ordinal)
                .ToArray();
        return frozen.Length == RequiredBindingKeys.Length &&
            frozen.Select(binding => binding.Key)
                .SequenceEqual(
                    RequiredBindingKeys,
                    StringComparer.Ordinal) &&
            frozen.Zip(expected).All(pair =>
                string.Equals(
                    pair.First.Key,
                    pair.Second.Key,
                    StringComparison.Ordinal) &&
                pair.First.Version == pair.Second.Version &&
                string.Equals(
                    pair.First.Sha256,
                    pair.Second.Sha256,
                    StringComparison.Ordinal));
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

    public static bool IsValidStaffApproval(
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
        HasValidBindings(evidence, minimumCount: 4) &&
        RequiredBindingKeys.All(key =>
            evidence.StateBindings!.Any(binding =>
                string.Equals(
                    binding.Key,
                    key,
                    StringComparison.Ordinal))) &&
        IsSha256(evidence.StateBindingsSha256) &&
        IsSha256(evidence.ContentSha256);

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

    private static string ComputeRecordDigest(StaffMember member)
    {
        List<string> values =
        [
            member.Id.ToString("N"),
            member.Version.ToString(CultureInfo.InvariantCulture),
            ((int)member.Status).ToString(CultureInfo.InvariantCulture),
            Coordinate(member.DepartedAtUtc),
            member.DepartureEffectiveOn?.ToString(
                "O",
                CultureInfo.InvariantCulture) ?? "-"
        ];
        foreach (StaffPropertyAssignment assignment in member.Assignments
            .OrderBy(assignment => assignment.Id))
        {
            values.Add(assignment.Id.ToString("N"));
            values.Add(assignment.PropertyId.ToString("N"));
            values.Add(assignment.IsCurrent ? "1" : "0");
            values.Add(assignment.IsPrimary ? "1" : "0");
            values.Add(assignment.EffectiveFrom.ToString(
                "O",
                CultureInfo.InvariantCulture));
            values.Add(assignment.EffectiveTo?.ToString(
                "O",
                CultureInfo.InvariantCulture) ?? "-");
            values.Add(assignment.AssignedAtVersion.ToString(
                CultureInfo.InvariantCulture));
            values.Add(assignment.UnassignedAtVersion?.ToString(
                CultureInfo.InvariantCulture) ?? "-");
        }

        return Compute(values);
    }

    private static string ComputeGovernanceDigest(
        StaffEmploymentGovernance governance)
    {
        StaffEmploymentGovernanceBinding binding = governance.Binding;
        List<string> values =
        [
            governance.StaffMemberId.ToString("N"),
            governance.GovernanceContractVersion.ToString(
                CultureInfo.InvariantCulture),
            governance.SelectedStaffVersion.ToString(
                CultureInfo.InvariantCulture),
            governance.Version.ToString(CultureInfo.InvariantCulture),
            binding.OperatingCountryCode,
            binding.PolicyId,
            binding.PolicyVersion.ToString(CultureInfo.InvariantCulture),
            binding.DataRegionId,
            binding.TransferProfileId,
            binding.RetentionPolicyId,
            binding.RetentionPolicyVersion.ToString(
                CultureInfo.InvariantCulture),
            binding.ContentSha256,
            Coordinate(binding.PolicyEffectiveAtUtc),
            Coordinate(binding.PolicyExpiresAtUtc),
            Coordinate(binding.EvaluatedAtUtc),
            Coordinate(governance.ConfiguredAtUtc)
        ];
        foreach (StaffEmploymentGovernanceAcknowledgement acknowledgement
            in governance.AcceptedAcknowledgements
                .OrderBy(
                    item => item.AcknowledgementId,
                    StringComparer.Ordinal)
                .ThenBy(item => item.AcknowledgementVersion))
        {
            values.Add(acknowledgement.AcknowledgementId);
            values.Add(acknowledgement.AcknowledgementVersion.ToString(
                CultureInfo.InvariantCulture));
        }

        return Compute(values);
    }

    private static string ComputeRestrictionDigest(
        StaffProcessingRestrictionProjection restriction) =>
        Compute(
        [
            restriction.StaffMemberId.ToString("N"),
            restriction.ContractVersion.ToString(
                CultureInfo.InvariantCulture),
            restriction.Revision.ToString(CultureInfo.InvariantCulture),
            restriction.ActiveRestrictionCount.ToString(
                CultureInfo.InvariantCulture),
            restriction.IsRestricted ? "1" : "0",
            Coordinate(restriction.LastTransitionAtUtc)
        ]);

    private static string ComputeHoldDigest(
        IReadOnlyCollection<StaffDataHold> holds)
    {
        List<string> values =
        [
            holds.Count.ToString(CultureInfo.InvariantCulture)
        ];
        foreach (StaffDataHold hold in holds.OrderBy(hold => hold.Id))
        {
            values.Add(hold.Id.ToString("N"));
            values.Add(hold.Version.ToString(CultureInfo.InvariantCulture));
            values.Add(((int)hold.State).ToString(
                CultureInfo.InvariantCulture));
            values.Add(Coordinate(hold.PlacedAtUtc));
            values.Add(Coordinate(hold.ReleasedAtUtc));
        }

        return Compute(values);
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
        value is { Length: DataRightsAnonymisationPolicyContract.Sha256Length } &&
        value.All(character =>
            character is (>= '0' and <= '9') or
                (>= 'a' and <= 'f'));
}
