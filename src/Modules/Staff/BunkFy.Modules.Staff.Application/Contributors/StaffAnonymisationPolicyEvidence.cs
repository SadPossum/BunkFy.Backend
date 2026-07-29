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
}
