namespace BunkFy.Modules.Staff.Application.Validation;

using BunkFy.DataGovernance;
using BunkFy.Modules.Staff.Application.Commands;
using BunkFy.Modules.Staff.Domain.Governance;
using Gma.Framework.Cqrs;

internal sealed class ConfigureStaffEmploymentGovernanceCommandValidator
    : ICommandValidator<ConfigureStaffEmploymentGovernanceCommand>
{
    public IEnumerable<string> Validate(
        ConfigureStaffEmploymentGovernanceCommand command)
    {
        if (command.IdempotencyKey == Guid.Empty)
        {
            yield return "IdempotencyKey is required.";
        }

        if (command.StaffMemberId == Guid.Empty)
        {
            yield return "StaffMemberId is required.";
        }

        if (command.ExpectedStaffVersion < 1)
        {
            yield return "ExpectedStaffVersion must be positive.";
        }

        if (command.ExpectedGovernanceVersion < 0)
        {
            yield return "ExpectedGovernanceVersion cannot be negative.";
        }

        if (command.OperatingCountryCode?.Trim().Length !=
            StaffEmploymentGovernanceBinding.CountryCodeLength)
        {
            yield return "OperatingCountryCode must be a two-letter code.";
        }

        if (!IsBoundedKey(command.PolicyId) ||
            !IsBoundedKey(command.DataRegionId) ||
            !IsBoundedKey(command.TransferProfileId) ||
            !IsBoundedKey(command.RetentionPolicyId))
        {
            yield return "One or more policy coordinates are invalid.";
        }

        if (command.PolicyVersion < 1 ||
            command.RetentionPolicyVersion < 1)
        {
            yield return "Policy versions must be positive.";
        }

        if (command.AcceptedAcknowledgements is null ||
            command.AcceptedAcknowledgements.Count >
                CountryPolicyRegistry.MaximumAcceptedAcknowledgements)
        {
            yield return "AcceptedAcknowledgements exceeds the supported limit.";
        }
        else if (command.AcceptedAcknowledgements.Any(
                     acknowledgement =>
                         !IsBoundedKey(
                             acknowledgement.AcknowledgementId) ||
                         acknowledgement.AcknowledgementVersion < 1))
        {
            yield return "One or more acknowledgements are invalid.";
        }

        foreach (string error in StaffValidation.Common(
                     version: null,
                     command.ActorId))
        {
            yield return error;
        }
    }

    private static bool IsBoundedKey(string? value)
    {
        string normalized = value?.Trim() ?? string.Empty;
        return normalized.Length is > 0 and <=
            StaffEmploymentGovernanceBinding.KeyMaxLength;
    }
}
