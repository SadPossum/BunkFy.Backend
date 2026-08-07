namespace BunkFy.Modules.Properties.Application.Handlers;

using System.Globalization;
using BunkFy.Modules.Properties.Domain.ValueObjects;

internal static class BedMutationFingerprint
{
    public static string ComputeAdd(
        Guid propertyId,
        Guid roomId,
        long expectedRoomVersion,
        BedLabel label) => PropertiesMutationFingerprint.Compute(
            "bunkfy-properties-bed-add/v1",
            propertyId.ToString("N"),
            roomId.ToString("N"),
            expectedRoomVersion.ToString(CultureInfo.InvariantCulture),
            label.Value);

    public static string ComputeBatchAdd(
        Guid propertyId,
        Guid roomId,
        long expectedRoomVersion,
        IReadOnlyCollection<BedLabel> labels)
    {
        List<string?> values =
        [
            "bunkfy-properties-bed-batch-add/v1",
            propertyId.ToString("N"),
            roomId.ToString("N"),
            expectedRoomVersion.ToString(CultureInfo.InvariantCulture),
            labels.Count.ToString(CultureInfo.InvariantCulture)
        ];
        values.AddRange(labels.Select(label => label.Value));
        return PropertiesMutationFingerprint.Compute([.. values]);
    }

    public static string ComputeUpdate(
        Guid propertyId,
        Guid roomId,
        Guid bedId,
        long expectedRoomVersion,
        BedLabel label) => PropertiesMutationFingerprint.Compute(
            "bunkfy-properties-bed-update/v1",
            propertyId.ToString("N"),
            roomId.ToString("N"),
            bedId.ToString("N"),
            expectedRoomVersion.ToString(CultureInfo.InvariantCulture),
            label.Value);
}
