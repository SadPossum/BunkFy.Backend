namespace BunkFy.Modules.Ingestion.Application.Reservations;

using System.Text.Json;
using System.Text.Json.Serialization;
using Gma.Framework.Results;
using BunkFy.Modules.Ingestion.Application.Commands;

internal static class ReservationObservationJsonNormalizer
{
    public const string RecordType = "reservation.v1";
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static Result<NormalizedReservationObservation> Normalize(ReadOnlySpan<byte> payload)
    {
        try
        {
            ReservationDocument? document = JsonSerializer.Deserialize<ReservationDocument>(payload, SerializerOptions);
            if (document is null)
            {
                return Invalid();
            }

            NormalizedReservationObservationKind kind = document.Operation switch
            {
                "upsert" => NormalizedReservationObservationKind.Upsert,
                "cancel" => NormalizedReservationObservationKind.Cancel,
                _ => NormalizedReservationObservationKind.Unknown
            };
            Guid[] units = document.InventoryUnitIds?.ToArray() ?? [];
            if (kind == NormalizedReservationObservationKind.Unknown || document.SourceSequence < 0 ||
                (kind == NormalizedReservationObservationKind.Upsert &&
                 (!document.Arrival.HasValue || !document.Departure.HasValue || document.Departure <= document.Arrival ||
                  units.Length == 0 || units.Any(id => id == Guid.Empty) || units.Distinct().Count() != units.Length ||
                  string.IsNullOrWhiteSpace(document.PrimaryGuestName) || document.GuestCount <= 0)))
            {
                return Invalid();
            }

            return Result.Success(new NormalizedReservationObservation(
                kind,
                document.SourceSequence,
                document.Arrival,
                document.Departure,
                Array.AsReadOnly(units),
                Normalize(document.PrimaryGuestName),
                Normalize(document.Email),
                Normalize(document.Phone),
                document.GuestCount,
                Normalize(document.Notes)));
        }
        catch (JsonException)
        {
            return Invalid();
        }
    }

    private static Result<NormalizedReservationObservation> Invalid() =>
        Result.Failure<NormalizedReservationObservation>(IngestionApplicationErrors.NormalizedReservationObservationInvalid);

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class ReservationDocument
    {
        [JsonPropertyName("operation")]
        public string? Operation { get; init; }

        [JsonPropertyName("sourceSequence")]
        public long? SourceSequence { get; init; }

        [JsonPropertyName("arrival")]
        public DateOnly? Arrival { get; init; }

        [JsonPropertyName("departure")]
        public DateOnly? Departure { get; init; }

        [JsonPropertyName("inventoryUnitIds")]
        public List<Guid>? InventoryUnitIds { get; init; }

        [JsonPropertyName("primaryGuestName")]
        public string? PrimaryGuestName { get; init; }

        [JsonPropertyName("email")]
        public string? Email { get; init; }

        [JsonPropertyName("phone")]
        public string? Phone { get; init; }

        [JsonPropertyName("guestCount")]
        public int? GuestCount { get; init; }

        [JsonPropertyName("notes")]
        public string? Notes { get; init; }
    }
}
