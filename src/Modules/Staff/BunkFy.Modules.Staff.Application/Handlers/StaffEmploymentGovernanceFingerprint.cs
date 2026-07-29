namespace BunkFy.Modules.Staff.Application.Handlers;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BunkFy.Modules.Staff.Application.Commands;

internal static class StaffEmploymentGovernanceFingerprint
{
    public static string Compute(
        ConfigureStaffEmploymentGovernanceCommand command,
        string actorId)
    {
        string acknowledgements = string.Join(
            '\n',
            command.AcceptedAcknowledgements
                .OrderBy(
                    item => item.AcknowledgementId,
                    StringComparer.Ordinal)
                .ThenBy(item => item.AcknowledgementVersion)
                .Select(item =>
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{item.AcknowledgementId}:{item.AcknowledgementVersion}")));
        string canonical = string.Join(
            '\n',
            command.StaffMemberId.ToString("D"),
            command.ExpectedStaffVersion.ToString(
                CultureInfo.InvariantCulture),
            command.ExpectedGovernanceVersion.ToString(
                CultureInfo.InvariantCulture),
            command.OperatingCountryCode,
            command.PolicyId,
            command.PolicyVersion.ToString(CultureInfo.InvariantCulture),
            command.DataRegionId,
            command.TransferProfileId,
            command.RetentionPolicyId,
            command.RetentionPolicyVersion.ToString(
                CultureInfo.InvariantCulture),
            acknowledgements,
            actorId);
        return Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
