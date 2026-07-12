namespace BunkFy.Modules.Ingestion.Application.Reservations;

using System.Text.Json;
using System.Text.Json.Serialization;
using BunkFy.Modules.Ingestion.Application.Commands;

internal sealed record ReservationOperationalBaseline
{
    public const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; }

    [JsonPropertyName("arrival")]
    public DateOnly Arrival { get; init; }

    [JsonPropertyName("departure")]
    public DateOnly Departure { get; init; }

    [JsonPropertyName("inventoryUnitIds")]
    public IReadOnlyCollection<Guid> InventoryUnitIds { get; init; } = [];

    public static string Serialize(NormalizedReservationObservation observation)
    {
        if (!TryCreate(observation, out ReservationOperationalBaseline? baseline))
        {
            throw new InvalidOperationException("A reservation operational baseline requires a valid upsert observation.");
        }

        return JsonSerializer.Serialize(baseline, SerializerOptions);
    }

    public static string? FromNormalizedSnapshot(string normalizedSnapshot)
    {
        try
        {
            NormalizedReservationObservation? observation =
                JsonSerializer.Deserialize<NormalizedReservationObservation>(normalizedSnapshot);
            return observation is not null && TryCreate(observation, out ReservationOperationalBaseline? baseline)
                ? JsonSerializer.Serialize(baseline, SerializerOptions)
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static ReservationOperationalBaseline? Deserialize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            ReservationOperationalBaseline? baseline =
                JsonSerializer.Deserialize<ReservationOperationalBaseline>(value, SerializerOptions);
            if (baseline is null || baseline.SchemaVersion != CurrentSchemaVersion ||
                baseline.Departure <= baseline.Arrival || baseline.InventoryUnitIds.Count == 0)
            {
                return null;
            }

            Guid[] canonicalUnits = baseline.InventoryUnitIds.Distinct().Order().ToArray();
            return canonicalUnits.Length == baseline.InventoryUnitIds.Count &&
                   canonicalUnits.SequenceEqual(baseline.InventoryUnitIds) &&
                   canonicalUnits.All(id => id != Guid.Empty)
                ? baseline
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryCreate(
        NormalizedReservationObservation observation,
        out ReservationOperationalBaseline? baseline)
    {
        Guid[] units = observation.InventoryUnitIds.Distinct().Order().ToArray();
        if (observation.Kind != NormalizedReservationObservationKind.Upsert ||
            !observation.Arrival.HasValue || !observation.Departure.HasValue ||
            observation.Departure <= observation.Arrival || units.Length == 0 ||
            units.Length != observation.InventoryUnitIds.Count || units.Any(id => id == Guid.Empty))
        {
            baseline = null;
            return false;
        }

        baseline = new ReservationOperationalBaseline
        {
            SchemaVersion = CurrentSchemaVersion,
            Arrival = observation.Arrival.Value,
            Departure = observation.Departure.Value,
            InventoryUnitIds = Array.AsReadOnly(units)
        };
        return true;
    }
}
