namespace BunkFy.Extensions.Operations.Notifications;

using System.Reflection;
using System.Security.Cryptography;
using BunkFy.DataGovernance;

internal static class OperationsNotificationsPersonalDataCatalog
{
    private const string CatalogResourceName =
        "BunkFy.Extensions.Operations.Notifications.DataGovernance.personal-data-catalog.v1.json";

    private static readonly Lazy<OperationsNotificationsPersonalDataCatalogEvidence>
        CurrentEvidence = new(Load);

    public static OperationsNotificationsPersonalDataCatalogEvidence Current =>
        CurrentEvidence.Value;

    private static OperationsNotificationsPersonalDataCatalogEvidence Load()
    {
        Assembly assembly = typeof(OperationsNotificationsPersonalDataCatalog).Assembly;
        using Stream stream =
            assembly.GetManifestResourceStream(CatalogResourceName) ??
            throw new InvalidDataException(
                "The Operations Notifications personal-data catalogue is unavailable.");
        using MemoryStream buffer = new();
        stream.CopyTo(buffer);
        byte[] content = buffer.ToArray();
        PersonalDataCatalogDocument catalog =
            PersonalDataCatalogJson.Parse(content);

        if (!string.Equals(
                catalog.CatalogId,
                "operations-notifications.personal-data",
                StringComparison.Ordinal) ||
            !string.Equals(
                catalog.Module,
                OperationsNotificationsDataRightsCoordinates.Owner,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The Operations Notifications personal-data catalogue identity is invalid.");
        }

        return new OperationsNotificationsPersonalDataCatalogEvidence(
            catalog,
            Convert.ToHexStringLower(SHA256.HashData(content)));
    }
}

internal sealed record OperationsNotificationsPersonalDataCatalogEvidence(
    PersonalDataCatalogDocument Document,
    string ContentSha256);
