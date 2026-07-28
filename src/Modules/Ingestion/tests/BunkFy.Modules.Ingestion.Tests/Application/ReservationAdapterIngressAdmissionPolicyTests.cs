namespace BunkFy.Modules.Ingestion.Tests.Application;

using System.Text;
using System.Text.Json;
using BunkFy.Adapter.Abstractions;
using BunkFy.Modules.Ingestion.Application.Reservations;
using BunkFy.Modules.Reservations.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ReservationAdapterIngressAdmissionPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 28, 12, 0, 0, TimeSpan.Zero);
    private readonly ReservationAdapterIngressAdmissionPolicy policy = new();

    [Fact]
    public void Canonical_upsert_and_minimal_cancellation_are_admitted()
    {
        AdapterIngressObservationRequest upsert = Create(
            """
            {"operation":"upsert","sourceSequence":7,"arrival":"2026-08-01","departure":"2026-08-03","expectedArrivalTime":"15:30:00","expectedDepartureTime":"10:45:00","inventoryUnitIds":["20000000-0000-0000-0000-000000000001"],"primaryGuestName":"Ada Guest","email":"ada@example.test","phone":"+44 20 1234 5678","guestCount":1,"notes":null}
            """);
        AdapterIngressObservationRequest cancellation = Create(
            """{"operation":"cancel","sourceSequence":8}""");

        Assert.True(this.policy.Validate([upsert, cancellation]).IsSuccess);
    }

    [Fact]
    public void Optional_contact_and_expected_times_may_be_absent()
    {
        AdapterIngressObservationRequest upsert = Create(
            """
            {"operation":"upsert","sourceSequence":7,"arrival":"2026-08-01","departure":"2026-08-03","inventoryUnitIds":["20000000-0000-0000-0000-000000000001"],"primaryGuestName":"Ada Guest","phone":null,"guestCount":1,"notes":null}
            """);

        Assert.True(this.policy.Validate([upsert]).IsSuccess);
    }

    [Theory]
    [InlineData("reservation.v2", "application/json")]
    [InlineData("reservation.v1", "application/json; charset=utf-8")]
    [InlineData("reservation.v1", "text/json")]
    public void Unknown_versions_and_content_types_are_rejected(string recordType, string contentType)
    {
        AdapterIngressObservationRequest request = Create(
            """{"operation":"cancel","sourceSequence":8}""",
            recordType,
            contentType);

        Assert.True(this.policy.Validate([request]).IsFailure);
    }

    [Fact]
    public void Hash_mismatch_is_rejected()
    {
        AdapterIngressObservationRequest request = Create(
            """{"operation":"cancel","sourceSequence":8}""") with
        {
            ContentSha256 = new string('0', AdapterProtocolLimits.Sha256Length)
        };

        Assert.True(this.policy.Validate([request]).IsFailure);
    }

    [Fact]
    public void Malformed_utf8_is_rejected()
    {
        byte[] payload = [0x7B, 0x22, 0x6F, 0x70, 0x22, 0x3A, 0xC3, 0x28, 0x7D];
        var request = new AdapterIngressObservationRequest(
            Guid.NewGuid(),
            "reservation.v1",
            "booking-42",
            "revision-7",
            Now,
            Now,
            "application/json",
            payload,
            AdapterPayloadHash.ComputeSha256(payload));

        Assert.True(this.policy.Validate([request]).IsFailure);
    }

    [Theory]
    [InlineData("""{"operation":"cancel","sourceSequence":8,"cardNumber":"4111111111111111"}""")]
    [InlineData("""{"operation":"cancel","sourceSequence":8,"providerCredential":"secret"}""")]
    [InlineData("""{"operation":"cancel","sourceSequence":8,"arrival":"2026-08-01"}""")]
    [InlineData("""{"operation":"upsert","sourceSequence":7,"arrival":"2026-08-01","departure":"2026-08-03","inventoryUnitIds":["20000000-0000-0000-0000-000000000001"],"primaryGuestName":"Ada Guest","guestCount":1,"notes":"card 4111111111111111"}""")]
    public void Unknown_prohibited_and_noncanonical_fields_are_rejected(string payload)
    {
        Assert.True(this.policy.Validate([Create(payload)]).IsFailure);
    }

    [Fact]
    public void Product_contract_bounds_are_enforced_before_persistence()
    {
        string name = new('a', ReservationsContractLimits.PrimaryGuestNameMaxLength + 1);
        AdapterIngressObservationRequest request = Create(
            $$"""
            {"operation":"upsert","sourceSequence":7,"arrival":"2026-08-01","departure":"2026-08-03","inventoryUnitIds":["20000000-0000-0000-0000-000000000001"],"primaryGuestName":"{{name}}","guestCount":1}
            """);

        Assert.True(this.policy.Validate([request]).IsFailure);
    }

    [Fact]
    public void Entire_batch_is_rejected_when_any_record_is_invalid()
    {
        AdapterIngressObservationRequest valid = Create(
            """{"operation":"cancel","sourceSequence":8}""");
        AdapterIngressObservationRequest invalid = Create(
            """{"operation":"cancel","sourceSequence":9}""") with
        {
            ContentSha256 = new string('0', AdapterProtocolLimits.Sha256Length)
        };

        Assert.True(this.policy.Validate([valid, invalid]).IsFailure);
    }

    [Fact]
    public void Public_request_envelopes_reject_unknown_members()
    {
        string json =
            """
            {"records":[],"unexpected":true}
            """;

        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<AdapterIngressSubmissionRequest>(json));
    }

    private static AdapterIngressObservationRequest Create(
        string payload,
        string recordType = "reservation.v1",
        string contentType = "application/json")
    {
        byte[] bytes = Encoding.UTF8.GetBytes(payload);
        return new AdapterIngressObservationRequest(
            Guid.NewGuid(),
            recordType,
            "booking-42",
            "revision-7",
            Now,
            Now,
            contentType,
            bytes,
            AdapterPayloadHash.ComputeSha256(bytes));
    }
}
