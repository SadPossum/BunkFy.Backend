namespace BunkFy.Modules.Properties.Application.Handlers;

using System.Globalization;
using BunkFy.Modules.Properties.Application.Commands;
using BunkFy.Modules.Properties.Contracts;

internal static class PropertyLifecycleMutationFingerprint
{
    public static string ComputeActivation(
        ActivatePropertyProcessingCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        List<string?> values =
        [
            "bunkfy-properties-processing-activation/v1",
            command.PropertyId.ToString("N"),
            command.ExpectedVersion.ToString(CultureInfo.InvariantCulture),
            command.OperatingCountryCode,
            command.PolicyId,
            command.PolicyVersion.ToString(CultureInfo.InvariantCulture),
            command.DataRegionId,
            command.TransferProfileId,
            command.RetentionPolicyId,
            command.RetentionPolicyVersion.ToString(
                CultureInfo.InvariantCulture)
        ];
        PropertyGovernanceAcknowledgementDto[] acknowledgements =
            command.AcceptedAcknowledgements?
                .OrderBy(
                    item => item.AcknowledgementId,
                    StringComparer.Ordinal)
                .ThenBy(item => item.AcknowledgementVersion)
                .ToArray() ?? [];
        values.Add(acknowledgements.Length.ToString(
            CultureInfo.InvariantCulture));
        foreach (PropertyGovernanceAcknowledgementDto acknowledgement in
                 acknowledgements)
        {
            values.Add(acknowledgement.AcknowledgementId);
            values.Add(acknowledgement.AcknowledgementVersion.ToString(
                CultureInfo.InvariantCulture));
        }

        return PropertiesMutationFingerprint.Compute([.. values]);
    }

    public static string ComputeSuspension(
        Guid propertyId,
        long expectedVersion) => PropertiesMutationFingerprint.Compute(
        "bunkfy-properties-processing-suspension/v1",
        propertyId.ToString("N"),
        expectedVersion.ToString(CultureInfo.InvariantCulture));

    public static string ComputeRetirement(
        Guid propertyId,
        long expectedVersion) => PropertiesMutationFingerprint.Compute(
        "bunkfy-properties-retirement/v1",
        propertyId.ToString("N"),
        expectedVersion.ToString(CultureInfo.InvariantCulture));
}
