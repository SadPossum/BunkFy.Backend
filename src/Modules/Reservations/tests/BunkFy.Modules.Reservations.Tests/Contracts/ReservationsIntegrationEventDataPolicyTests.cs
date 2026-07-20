namespace BunkFy.Modules.Reservations.Tests;

using System.Text.Json;
using BunkFy.Modules.Reservations.Contracts;
using Gma.Framework.Messaging;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ReservationsIntegrationEventDataPolicyTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly string[] DirectGuestFields =
    [
        "PrimaryGuestName",
        "Email",
        "Phone",
        "Notes",
    ];

    private static readonly Dictionary<Type, string[]> AllowedDirectGuestFields =
        new()
        {
            [typeof(ExternalReservationCreateRequestedIntegrationEvent)] = DirectGuestFields,
            [typeof(ExternalReservationAmendmentRequestedIntegrationEvent)] = DirectGuestFields,
            [typeof(ExternalReservationGuestDetailsChangeRequestedIntegrationEvent)] = DirectGuestFields,
        };

    [Fact]
    public void Every_reservations_event_has_an_explicit_direct_guest_field_classification()
    {
        Type[] eventTypes = typeof(ReservationsModuleMetadata).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && typeof(IIntegrationEvent).IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(eventTypes);
        foreach (Type eventType in eventTypes)
        {
            string[] actual = eventType.GetProperties()
                .Select(property => property.Name)
                .Intersect(DirectGuestFields, StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            string[] expected = AllowedDirectGuestFields.TryGetValue(eventType, out string[]? allowed)
                ? allowed.Order(StringComparer.Ordinal).ToArray()
                : [];

            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void Operational_rejection_reasons_cannot_be_free_form_text()
    {
        Type[] eventTypes = typeof(ReservationsModuleMetadata).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && typeof(IIntegrationEvent).IsAssignableFrom(type))
            .ToArray();

        foreach (System.Reflection.PropertyInfo property in eventTypes
                     .SelectMany(type => type.GetProperties())
                     .Where(property => property.Name.EndsWith("Reason", StringComparison.Ordinal)))
        {
            Type valueType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            Assert.True(valueType.IsEnum, $"{property.DeclaringType?.Name}.{property.Name} must use a bounded enum.");
        }
    }

    [Fact]
    public void Version_one_reminder_accepts_legacy_json_but_does_not_retain_the_guest_name()
    {
        Guid eventId = Guid.NewGuid();
        Guid reservationId = Guid.NewGuid();
        Guid propertyId = Guid.NewGuid();
        string json = $$"""
            {
              "eventId": "{{eventId}}",
              "tenantId": "tenant-a",
              "occurredAtUtc": "2026-07-16T10:30:00+00:00",
              "eventName": "reservation-arrival-reminder-due",
              "version": 1,
              "reservationId": "{{reservationId}}",
              "propertyId": "{{propertyId}}",
              "primaryGuestName": "Maya Chen",
              "arrival": "2026-07-16",
              "expectedArrivalTime": "15:30:00",
              "timeZoneId": "Europe/Moscow",
              "detailsRevision": 3
            }
            """;

        ReservationArrivalReminderDueIntegrationEvent? integrationEvent =
            JsonSerializer.Deserialize<ReservationArrivalReminderDueIntegrationEvent>(
                json,
                JsonOptions);

        Assert.NotNull(integrationEvent);
        Assert.Equal(reservationId, integrationEvent.ReservationId);
        Assert.DoesNotContain(
            "primaryGuestName",
            JsonSerializer.Serialize(integrationEvent, JsonOptions),
            StringComparison.OrdinalIgnoreCase);
    }
}
