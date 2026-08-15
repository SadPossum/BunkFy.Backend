namespace BunkFy.TimeZones.Tests;

using System.Security.Cryptography;
using System.Text;
using Xunit;

public sealed class TimeZoneCatalogTests
{
    private const int PersistedCatalogVersionMaxLength = 64;
    private const int PersistedTimeZoneIdMaxLength = 128;
    private const int ApiCommentMaxLength = 2048;
    private const int ApiCountryNameMaxLength = 128;
    private const int IsoCountryCountUpperBound = 249;
    private readonly TimeZoneCatalog catalog = TimeZoneCatalog.Default;

    [Fact]
    public void Default_is_one_immutable_pinned_catalog()
    {
        Assert.Same(TimeZoneCatalog.Default, TimeZoneCatalog.Default);
        Assert.Equal("2026c", this.catalog.TzdbVersion);
        Assert.StartsWith("TZDB: 2026c", this.catalog.CatalogVersion);
        Assert.Equal(
            this.catalog.CanonicalIds.OrderBy(value => value, StringComparer.Ordinal),
            this.catalog.CanonicalIds);
    }

    [Theory]
    [InlineData("Etc/UTC", "Etc/UTC", TimeZoneCatalogResolutionKind.Canonical)]
    [InlineData("UTC", "Etc/UTC", TimeZoneCatalogResolutionKind.Alias)]
    [InlineData("Asia/Calcutta", "Asia/Kolkata", TimeZoneCatalogResolutionKind.Alias)]
    [InlineData("Asia/Kolkata", "Asia/Kolkata", TimeZoneCatalogResolutionKind.Canonical)]
    public void Resolve_uses_primary_tzdb_identifiers(
        string input,
        string expectedCanonical,
        TimeZoneCatalogResolutionKind expectedKind)
    {
        Assert.True(this.catalog.TryResolve(input, out TimeZoneCatalogResolution? result));
        Assert.NotNull(result);
        Assert.Equal(expectedCanonical, result.CanonicalTimeZoneId);
        Assert.Equal(expectedKind, result.Kind);
    }

    [Theory]
    [InlineData("Pacific Standard Time")]
    [InlineData("Missing/Zone")]
    [InlineData("asia/kolkata")]
    [InlineData("")]
    public void Resolve_rejects_values_outside_the_embedded_tzdb(string input) =>
        Assert.False(this.catalog.TryResolve(input, out _));

    [Theory]
    [InlineData("Pacific Standard Time", true)]
    [InlineData("pacific standard time", false)]
    [InlineData("Custom/OperatorZone", false)]
    [InlineData("Etc/UTC", false)]
    public void Windows_identifier_classification_uses_the_embedded_mapping(
        string input,
        bool expected) =>
        Assert.Equal(expected, this.catalog.IsKnownWindowsId(input));

    [Fact]
    public void Describe_uses_one_explicit_instant_and_country_metadata()
    {
        TimeZoneCatalogEntry winter = this.catalog.Describe(
            "Europe/London",
            new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));
        TimeZoneCatalogEntry summer = this.catalog.Describe(
            "Europe/London",
            new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero));

        Assert.Contains(winter.Countries, country => country.Code == "GB");
        Assert.Equal(0, winter.UtcOffsetMinutes);
        Assert.Equal(60, summer.UtcOffsetMinutes);
    }

    [Fact]
    public void Every_exposed_identifier_is_a_primary_identifier()
    {
        foreach (string identifier in this.catalog.CanonicalIds)
        {
            Assert.True(this.catalog.TryResolve(
                identifier,
                out TimeZoneCatalogResolution? result));
            Assert.NotNull(result);
            Assert.Equal(TimeZoneCatalogResolutionKind.Canonical, result.Kind);
            Assert.Equal(identifier, result.CanonicalTimeZoneId);
        }
    }

    [Fact]
    public void Resolution_map_is_complete_ordered_bounded_and_pinned()
    {
        Assert.Equal(597, this.catalog.Resolutions.Count);
        Assert.Equal(
            this.catalog.Resolutions
                .OrderBy(
                    resolution => resolution.RequestedTimeZoneId,
                    StringComparer.Ordinal),
            this.catalog.Resolutions);
        Assert.All(this.catalog.Resolutions, resolution =>
        {
            Assert.InRange(
                resolution.RequestedTimeZoneId.Length,
                1,
                PersistedTimeZoneIdMaxLength);
            Assert.InRange(
                resolution.CanonicalTimeZoneId.Length,
                1,
                PersistedTimeZoneIdMaxLength);
            Assert.DoesNotContain(
                "Standard Time",
                resolution.RequestedTimeZoneId,
                StringComparison.Ordinal);
        });

        string artifact = string.Join(
            '\n',
            this.catalog.Resolutions.Select(resolution =>
                $"{resolution.RequestedTimeZoneId}={resolution.CanonicalTimeZoneId}"));
        string digest = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(artifact)));
        Assert.Equal(
            "b363abb838dfbcafdd96c74343213af659bc714d04bb80e8e8516b62c07f6d02",
            digest);
    }

    [Fact]
    public void Embedded_catalog_fits_persistence_and_api_contract_bounds()
    {
        Assert.InRange(
            this.catalog.CatalogVersion.Length,
            1,
            PersistedCatalogVersionMaxLength);
        Assert.All(this.catalog.CanonicalIds, identifier =>
            Assert.InRange(
                identifier.Length,
                1,
                PersistedTimeZoneIdMaxLength));

        DateTimeOffset observedAtUtc =
            new(2026, 8, 13, 0, 0, 0, TimeSpan.Zero);
        Assert.All(this.catalog.CanonicalIds, identifier =>
        {
            TimeZoneCatalogEntry entry = this.catalog.Describe(
                identifier,
                observedAtUtc);
            Assert.True(entry.Comment is null ||
                entry.Comment.Length <= ApiCommentMaxLength);
            Assert.InRange(
                entry.Countries.Count,
                0,
                IsoCountryCountUpperBound);
            Assert.All(entry.Countries, country =>
            {
                Assert.Matches("^[A-Z]{2}$", country.Code);
                Assert.InRange(
                    country.Name.Length,
                    1,
                    ApiCountryNameMaxLength);
            });
        });
    }

    [Fact]
    public void Runtime_probe_fails_closed_on_an_embedded_offset_mismatch()
    {
        int resolverCalls = 0;
        var probe = new TimeZoneRuntimeCompatibilityProbe(identifier =>
        {
            resolverCalls++;
            Assert.Equal("Europe/London", identifier);
            return TimeZoneInfo.CreateCustomTimeZone(
                "Deliberately mismatched test zone",
                TimeSpan.FromHours(9),
                "Deliberately mismatched test zone",
                "Deliberately mismatched test zone");
        });

        bool compatible = probe.IsCompatible(
            "Europe/London",
            new DateTimeOffset(2026, 8, 13, 0, 0, 0, TimeSpan.Zero));

        Assert.False(compatible);
        Assert.Equal(1, resolverCalls);
    }

    [Fact]
    public void Runtime_probe_compares_transitions_and_caches_by_observation_year()
    {
        int resolverCalls = 0;
        var probe = new TimeZoneRuntimeCompatibilityProbe(identifier =>
        {
            resolverCalls++;
            return TimeZoneInfo.FindSystemTimeZoneById(identifier);
        });

        bool first = probe.IsCompatible(
            "Etc/UTC",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        bool sameYear = probe.IsCompatible(
            "Etc/UTC",
            new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero));

        Assert.True(first);
        Assert.True(sameYear);
        Assert.Equal(1, resolverCalls);
    }

    [Fact]
    public void Runtime_probe_accepts_a_real_stable_dst_zone()
    {
        var probe = new TimeZoneRuntimeCompatibilityProbe();

        Assert.True(probe.IsCompatible(
            "Europe/London",
            new DateTimeOffset(2026, 8, 13, 0, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void Runtime_probe_detects_a_short_transition_present_only_on_the_runtime()
    {
        TimeZoneInfo.TransitionTime starts =
            TimeZoneInfo.TransitionTime.CreateFixedDateRule(
                new DateTime(1, 1, 1, 0, 1, 0),
                6,
                1);
        TimeZoneInfo.TransitionTime ends =
            TimeZoneInfo.TransitionTime.CreateFixedDateRule(
                new DateTime(1, 1, 1, 0, 2, 0),
                6,
                1);
        TimeZoneInfo.AdjustmentRule rule =
            TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
                new DateTime(2026, 1, 1),
                new DateTime(2026, 12, 31),
                TimeSpan.FromHours(1),
                starts,
                ends);
        TimeZoneInfo runtime = TimeZoneInfo.CreateCustomTimeZone(
            "Runtime with a one-minute-only transition",
            TimeSpan.Zero,
            "Runtime with a one-minute-only transition",
            "Runtime standard",
            "Runtime daylight",
            [rule]);
        var probe = new TimeZoneRuntimeCompatibilityProbe(_ => runtime);

        Assert.False(probe.IsCompatible(
            "Etc/UTC",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));
    }

    [Theory]
    [InlineData("UTC")]
    [InlineData("Pacific Standard Time")]
    [InlineData("Missing/Zone")]
    public void Runtime_probe_accepts_only_primary_tzdb_identifiers(
        string identifier)
    {
        var probe = new TimeZoneRuntimeCompatibilityProbe(_ =>
            throw new InvalidOperationException(
                "Invalid semantic input must not reach the runtime."));

        Assert.False(probe.IsCompatible(
            identifier,
            new DateTimeOffset(2026, 8, 13, 0, 0, 0, TimeSpan.Zero)));
    }
}
