namespace Properties.Application.Validation;

using Properties.Domain.Aggregates;

internal static class PropertiesValidation
{
    public static IEnumerable<string> ValidatePropertyWrite(string? name, string? code, string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            yield return "Property name is required.";
        }
        else if (name.Trim().Length > Property.PropertyNameMaxLength)
        {
            yield return $"Property name cannot exceed {Property.PropertyNameMaxLength} characters.";
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            yield return "Property code is required.";
        }
        else if (code.Trim().Length > Property.PropertyCodeMaxLength)
        {
            yield return $"Property code cannot exceed {Property.PropertyCodeMaxLength} characters.";
        }

        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            yield return "Time zone id is required.";
        }
        else if (timeZoneId.Trim().Length > Property.TimeZoneIdMaxLength)
        {
            yield return $"Time zone id cannot exceed {Property.TimeZoneIdMaxLength} characters.";
        }
    }

    public static IEnumerable<string> ValidateRoomWrite(string? name, string? buildingLabel, string? floorLabel)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            yield return "Room name is required.";
        }
        else if (name.Trim().Length > Room.RoomNameMaxLength)
        {
            yield return $"Room name cannot exceed {Room.RoomNameMaxLength} characters.";
        }

        if (!string.IsNullOrWhiteSpace(buildingLabel) && buildingLabel.Trim().Length > Room.PhysicalLabelMaxLength)
        {
            yield return $"Building label cannot exceed {Room.PhysicalLabelMaxLength} characters.";
        }

        if (!string.IsNullOrWhiteSpace(floorLabel) && floorLabel.Trim().Length > Room.PhysicalLabelMaxLength)
        {
            yield return $"Floor label cannot exceed {Room.PhysicalLabelMaxLength} characters.";
        }
    }

    public static IEnumerable<string> ValidateBedWrite(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            yield return "Bed label is required.";
        }
        else if (label.Trim().Length > Room.BedLabelMaxLength)
        {
            yield return $"Bed label cannot exceed {Room.BedLabelMaxLength} characters.";
        }
    }
}
