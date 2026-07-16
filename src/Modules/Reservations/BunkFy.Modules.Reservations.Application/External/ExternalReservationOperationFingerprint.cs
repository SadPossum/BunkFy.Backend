namespace BunkFy.Modules.Reservations.Application.External;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BunkFy.Modules.Reservations.Contracts;

internal static class ExternalReservationOperationFingerprint
{
    public static string Create(ExternalReservationCreateRequestedIntegrationEvent request) => Compute(
        request.OperationId,
        request.PropertyId,
        request.SourceSystem,
        request.SourceReference,
        request.Arrival.ToString("O", CultureInfo.InvariantCulture),
        request.Departure.ToString("O", CultureInfo.InvariantCulture),
        FormatTime(request.ExpectedArrivalTime),
        FormatTime(request.ExpectedDepartureTime),
        string.Join(',', request.InventoryUnitIds.Order().Select(id => id.ToString("N"))),
        request.PrimaryGuestName,
        request.Email,
        request.Phone,
        request.GuestCount.ToString(CultureInfo.InvariantCulture),
        request.Notes,
        FormatTime(request.ExpectedArrivalTime),
        FormatTime(request.ExpectedDepartureTime));

    public static string Change(ExternalReservationGuestDetailsChangeRequestedIntegrationEvent request) => Compute(
        request.OperationId,
        request.PropertyId,
        request.ReservationId,
        request.SourceSystem,
        request.SourceReference,
        request.ExpectedDetailsRevision.ToString(CultureInfo.InvariantCulture),
        request.PrimaryGuestName,
        request.Email,
        request.Phone,
        request.GuestCount.ToString(CultureInfo.InvariantCulture),
        request.Notes);

    public static string Amend(ExternalReservationAmendmentRequestedIntegrationEvent request) => Compute(
        request.OperationId,
        request.PropertyId,
        request.ReservationId,
        request.SourceSystem,
        request.SourceReference,
        request.ExpectedDetailsRevision.ToString(CultureInfo.InvariantCulture),
        request.Arrival.ToString("O", CultureInfo.InvariantCulture),
        request.Departure.ToString("O", CultureInfo.InvariantCulture),
        FormatTime(request.ExpectedArrivalTime),
        FormatTime(request.ExpectedDepartureTime),
        string.Join(',', request.InventoryUnitIds.Order().Select(id => id.ToString("N"))),
        request.PrimaryGuestName,
        request.Email,
        request.Phone,
        request.GuestCount.ToString(CultureInfo.InvariantCulture),
        request.Notes);

    public static string Cancel(ExternalReservationCancellationRequestedIntegrationEvent request) => Compute(
        request.OperationId,
        request.PropertyId,
        request.ReservationId,
        request.SourceSystem,
        request.SourceReference,
        request.ExpectedDetailsRevision.ToString(CultureInfo.InvariantCulture));

    private static string Compute(params object?[] values)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> length = stackalloc byte[sizeof(int)];
        foreach (object? value in values)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value?.ToString()?.Trim() ?? string.Empty);
            BitConverter.TryWriteBytes(length, bytes.Length);
            hash.AppendData(length);
            hash.AppendData(bytes);
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static string FormatTime(TimeOnly? value) =>
        value?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? string.Empty;
}
