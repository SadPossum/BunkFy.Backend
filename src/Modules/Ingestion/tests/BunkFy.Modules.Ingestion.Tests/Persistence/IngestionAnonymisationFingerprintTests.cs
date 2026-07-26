namespace BunkFy.Modules.Ingestion.Tests.Persistence;

using BunkFy.Modules.Ingestion.Domain.DataRights;
using BunkFy.Modules.Ingestion.Persistence;
using Microsoft.Extensions.Options;
using Xunit;

[Trait("Category", "Unit")]
public sealed class IngestionAnonymisationFingerprintTests
{
    [Fact]
    public void Fingerprints_are_deterministic_and_domain_separated()
    {
        HmacIngestionAnonymisationFingerprintService service =
            new(Options.Create(OptionsForKeys()));
        Guid connectionId =
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        var first = service.CreateActive(
            "tenant-a",
            IngestionAnonymisationFingerprintPurpose.ObservationReceipt,
            connectionId,
            "Reservation.V1",
            "booking-42");
        var replay = service.CreateActive(
            "tenant-a",
            IngestionAnonymisationFingerprintPurpose.ObservationReceipt,
            connectionId,
            "reservation.v1",
            "booking-42");
        var otherTenant = service.CreateActive(
            "tenant-b",
            IngestionAnonymisationFingerprintPurpose.ObservationReceipt,
            connectionId,
            "reservation.v1",
            "booking-42");
        var otherPurpose = service.CreateActive(
            "tenant-a",
            IngestionAnonymisationFingerprintPurpose.SourceLink,
            connectionId,
            "reservation.v1",
            "booking-42");

        Assert.True(first.IsSuccess);
        Assert.Equal(first.Value, replay.Value);
        Assert.NotEqual(first.Value.Sha256, otherTenant.Value.Sha256);
        Assert.NotEqual(first.Value.Sha256, otherPurpose.Value.Sha256);
        Assert.Equal(64, first.Value.Sha256.Length);
        Assert.Equal(first.Value.Sha256.ToLowerInvariant(), first.Value.Sha256);
    }

    [Fact]
    public void Candidate_matching_spans_key_rotation()
    {
        IngestionAnonymisationFingerprintOptions options = OptionsForKeys();
        options.Keys[2] = Convert.ToBase64String(
            Enumerable.Range(33, 32)
                .Select(value => (byte)value)
                .ToArray());
        HmacIngestionAnonymisationFingerprintService service =
            new(Options.Create(options));

        var candidates = service.CreateCandidates(
            "tenant-a",
            IngestionAnonymisationFingerprintPurpose.SourceLink,
            Guid.NewGuid(),
            "booking.com",
            "booking-42");

        Assert.True(candidates.IsSuccess);
        Assert.Equal([1, 2], candidates.Value.Select(item => item.KeyVersion));
        Assert.Equal(
            2,
            candidates.Value.Select(item => item.Sha256).Distinct().Count());
    }

    [Fact]
    public void Production_rejects_the_development_key()
    {
        IngestionAnonymisationFingerprintOptions options = new()
        {
            ActiveKeyVersion = 1,
            Keys =
            {
                [1] = IngestionAnonymisationFingerprintOptions
                    .DevelopmentKeyBase64
            }
        };

        ValidateOptionsResult result =
            new IngestionAnonymisationFingerprintOptionsValidator(
                isProduction: true)
            .Validate(name: null, options);

        Assert.True(result.Failed);
    }

    [Fact]
    public void Active_fingerprint_creation_fails_when_key_is_missing()
    {
        IngestionAnonymisationFingerprintOptions options =
            OptionsForKeys();
        options.ActiveKeyVersion = 2;
        HmacIngestionAnonymisationFingerprintService service =
            new(Options.Create(options));

        var result = service.CreateActive(
            "tenant-a",
            IngestionAnonymisationFingerprintPurpose.SourceLink,
            Guid.NewGuid(),
            "booking.com",
            "booking-42");

        Assert.True(result.IsFailure);
        Assert.Equal(
            BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors
                .AnonymisationFingerprintKeyUnavailable,
            result.Error);
    }

    private static IngestionAnonymisationFingerprintOptions
        OptionsForKeys() =>
        new()
        {
            ActiveKeyVersion = 1,
            Keys =
            {
                [1] = Convert.ToBase64String(
                    Enumerable.Range(1, 32)
                        .Select(value => (byte)value)
                        .ToArray())
            }
        };
}
