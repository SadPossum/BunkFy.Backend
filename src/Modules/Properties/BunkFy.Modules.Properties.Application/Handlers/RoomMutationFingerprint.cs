namespace BunkFy.Modules.Properties.Application.Handlers;

using System.Globalization;
using BunkFy.Modules.Properties.Domain.ValueObjects;

internal static class RoomMutationFingerprint
{
    public static string ComputeCreate(
        Guid propertyId,
        long expectedPropertyVersion,
        RoomDefinition definition) => PropertiesMutationFingerprint.Compute(
            "bunkfy-properties-room-create/v1",
            propertyId.ToString("N"),
            expectedPropertyVersion.ToString(CultureInfo.InvariantCulture),
            definition.Name.Value,
            definition.BuildingLabel?.Value,
            definition.FloorLabel?.Value);

    public static string ComputeUpdate(
        Guid propertyId,
        Guid roomId,
        long expectedRoomVersion,
        RoomDefinition definition) => PropertiesMutationFingerprint.Compute(
            "bunkfy-properties-room-update/v1",
            propertyId.ToString("N"),
            roomId.ToString("N"),
            expectedRoomVersion.ToString(CultureInfo.InvariantCulture),
            definition.Name.Value,
            definition.BuildingLabel?.Value,
            definition.FloorLabel?.Value);
}
