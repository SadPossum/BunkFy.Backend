namespace BunkFy.Modules.Ingestion.Application;

using System.Security.Cryptography;
using System.Text;
using BunkFy.Modules.Ingestion.Domain.Reservations;

internal static class ReservationOperationIdentity
{
    public static Guid CreateSourceLinkId(string scopeId, Guid connectionId, string sourceReference) =>
        Create("source-link", scopeId, connectionId.ToString("N"), sourceReference);

    public static Guid CreateOperationId(Guid receiptId, ReservationDispatchKind kind) =>
        Create("reservation-operation", receiptId.ToString("N"), ((int)kind).ToString(System.Globalization.CultureInfo.InvariantCulture));

    public static Guid CreateProposalId(Guid receiptId) =>
        Create("reservation-proposal", receiptId.ToString("N"));

    public static Guid CreateProposalOperationId(
        Guid proposalId,
        long proposalVersion,
        long reservationDetailsRevision) =>
        Create(
            "reservation-proposal-operation",
            proposalId.ToString("N"),
            proposalVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            reservationDetailsRevision.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private static Guid Create(params string[] components)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> length = stackalloc byte[sizeof(int)];
        foreach (string component in components)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(component);
            BitConverter.TryWriteBytes(length, bytes.Length);
            hash.AppendData(length);
            hash.AppendData(bytes);
        }

        byte[] digest = hash.GetHashAndReset();
        return new Guid(digest.AsSpan(0, 16));
    }
}
