namespace BunkFy.Modules.Ingestion.Persistence;

using System.Globalization;
using BunkFy.Modules.Ingestion.Application.Ports;
using Microsoft.Extensions.Configuration;

internal sealed class ConfiguredIngestionRetentionPolicy : IIngestionRetentionPolicy
{
    public const string RawPayloadConfigurationKey = "Ingestion:Retention:RawPayloadRetention";
    public const string SensitiveHistoryConfigurationKey = "Ingestion:Retention:SensitiveHistoryRetention";
    public const string LegalHoldReviewConfigurationKey =
        "Ingestion:Retention:LegalHoldReviewInterval";
    public static readonly TimeSpan DefaultRawPayloadRetention = TimeSpan.FromDays(30);
    public static readonly TimeSpan DefaultSensitiveHistoryRetention = TimeSpan.FromDays(90);
    public static readonly TimeSpan DefaultLegalHoldReviewInterval =
        TimeSpan.FromDays(30);
    private static readonly TimeSpan MinimumRetention = TimeSpan.FromHours(1);
    private static readonly TimeSpan MaximumRetention = TimeSpan.FromDays(3650);

    private readonly TimeSpan rawPayloadRetention;
    private readonly TimeSpan sensitiveHistoryRetention;
    private readonly TimeSpan legalHoldReviewInterval;

    private ConfiguredIngestionRetentionPolicy(
        TimeSpan rawPayloadRetention,
        TimeSpan sensitiveHistoryRetention,
        TimeSpan legalHoldReviewInterval)
    {
        this.rawPayloadRetention = rawPayloadRetention;
        this.sensitiveHistoryRetention = sensitiveHistoryRetention;
        this.legalHoldReviewInterval = legalHoldReviewInterval;
    }

    public static ConfiguredIngestionRetentionPolicy FromConfiguration(IConfiguration configuration)
    {
        return new ConfiguredIngestionRetentionPolicy(
            Parse(configuration, RawPayloadConfigurationKey, DefaultRawPayloadRetention),
            Parse(configuration, SensitiveHistoryConfigurationKey, DefaultSensitiveHistoryRetention),
            Parse(
                configuration,
                LegalHoldReviewConfigurationKey,
                DefaultLegalHoldReviewInterval));
    }

    public DateTimeOffset GetRawPayloadRetainUntilUtc(
        Guid propertyId,
        Guid connectionId,
        DateTimeOffset receivedAtUtc)
    {
        EnsureIdentity(propertyId, connectionId);
        return receivedAtUtc.Add(this.rawPayloadRetention);
    }

    public DateTimeOffset GetSensitiveHistoryRetainUntilUtc(
        Guid propertyId,
        Guid connectionId,
        DateTimeOffset terminalAtUtc)
    {
        EnsureIdentity(propertyId, connectionId);
        return terminalAtUtc.Add(this.sensitiveHistoryRetention);
    }

    public DateTimeOffset GetLegalHoldReviewDueAtUtc(
        DateTimeOffset placedAtUtc)
    {
        if (placedAtUtc == default)
        {
            throw new ArgumentException(
                "A legal-hold placement timestamp is required.",
                nameof(placedAtUtc));
        }

        return placedAtUtc.Add(this.legalHoldReviewInterval);
    }

    private static TimeSpan Parse(IConfiguration configuration, string key, TimeSpan defaultValue)
    {
        string? configured = configuration[key];
        TimeSpan retention = string.IsNullOrWhiteSpace(configured)
            ? defaultValue
            : TimeSpan.TryParse(configured, CultureInfo.InvariantCulture, out TimeSpan parsed)
                ? parsed
                : throw new InvalidOperationException($"{key} must be a valid invariant TimeSpan.");

        if (retention < MinimumRetention || retention > MaximumRetention)
        {
            throw new InvalidOperationException($"{key} must be between {MinimumRetention} and {MaximumRetention}.");
        }

        return retention;
    }

    private static void EnsureIdentity(Guid propertyId, Guid connectionId)
    {
        if (propertyId == Guid.Empty || connectionId == Guid.Empty)
        {
            throw new ArgumentException("A complete ingestion identity is required to resolve retention.");
        }
    }
}
