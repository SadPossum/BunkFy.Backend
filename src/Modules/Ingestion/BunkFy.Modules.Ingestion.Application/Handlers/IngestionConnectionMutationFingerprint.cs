namespace BunkFy.Modules.Ingestion.Application.Handlers;

using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BunkFy.Modules.Ingestion.Domain.Connections;

internal static class IngestionConnectionMutationFingerprint
{
    public static string ComputeCreate(AdapterConnection connection) => Compute(
        "bunkfy-ingestion-connection-create/v1",
        connection.PropertyId.ToString("N"),
        connection.AdapterType,
        ((int)connection.ExecutionMode).ToString(CultureInfo.InvariantCulture),
        ((int)connection.ConflictPolicy).ToString(CultureInfo.InvariantCulture),
        connection.ConfigurationReference,
        connection.SecretReference ?? string.Empty);

    private static string Compute(params string[] values)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(
            HashAlgorithmName.SHA256);
        Span<byte> length = stackalloc byte[sizeof(int)];
        foreach (string value in values)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
            hash.AppendData(length);
            hash.AppendData(bytes);
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
}
