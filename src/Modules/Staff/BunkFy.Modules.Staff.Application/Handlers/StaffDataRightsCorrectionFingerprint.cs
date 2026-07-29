namespace BunkFy.Modules.Staff.Application.Handlers;

using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;
using BunkFy.Modules.Staff.Domain.ValueObjects;

internal static class StaffDataRightsCorrectionFingerprint
{
    public static string Compute(StaffProfileCorrection correction)
    {
        ArgumentNullException.ThrowIfNull(correction);
        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("displayName", correction.DisplayName);
            WriteOptional(writer, "legalName", correction.LegalName);
            WriteOptional(writer, "workEmail", correction.WorkEmail);
            WriteOptional(writer, "workPhone", correction.WorkPhone);
            WriteOptional(writer, "employeeNumber", correction.EmployeeNumber);
            WriteOptional(writer, "jobTitle", correction.JobTitle);
            WriteOptional(writer, "department", correction.Department);
            writer.WriteEndObject();
        }

        return Convert.ToHexString(SHA256.HashData(buffer.WrittenSpan))
            .ToLowerInvariant();
    }

    private static void WriteOptional(
        Utf8JsonWriter writer,
        string propertyName,
        string? value)
    {
        if (value is null)
        {
            writer.WriteNull(propertyName);
        }
        else
        {
            writer.WriteString(propertyName, value);
        }
    }
}
