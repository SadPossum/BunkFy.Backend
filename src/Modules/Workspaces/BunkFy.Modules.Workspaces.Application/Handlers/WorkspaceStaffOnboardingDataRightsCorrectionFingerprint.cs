namespace BunkFy.Modules.Workspaces.Application.Handlers;

using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;
using BunkFy.Modules.Workspaces.Domain;

internal static class WorkspaceStaffOnboardingDataRightsCorrectionFingerprint
{
    public static string Compute(WorkspaceStaffApplicantProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("displayName", profile.DisplayName);
            WriteOptional(writer, "legalName", profile.LegalName);
            WriteOptional(writer, "workEmail", profile.WorkEmail);
            WriteOptional(writer, "workPhone", profile.WorkPhone);
            WriteOptional(writer, "employeeNumber", profile.EmployeeNumber);
            WriteOptional(writer, "jobTitle", profile.JobTitle);
            WriteOptional(writer, "department", profile.Department);
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
