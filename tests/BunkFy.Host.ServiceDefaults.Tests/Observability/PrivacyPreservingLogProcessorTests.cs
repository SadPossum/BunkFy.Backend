namespace BunkFy.Host.ServiceDefaults.Tests.Observability;

using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using BunkFy.Host.ServiceDefaults.Observability;
using Xunit;

[Trait("Category", "Unit")]
public sealed class PrivacyPreservingLogProcessorTests
{
    [Fact]
    public void Exported_log_keeps_template_and_bounded_dimensions_without_exception_or_personal_data()
    {
        const string reservationIdentifierCanary = "reservation-sensitive-491";
        const string exceptionCanary = "private.person@example.test could not be loaded";

        CapturingLogExporter exporter = new();
        using ILoggerFactory loggerFactory = LoggerFactory.Create(logging =>
            logging.AddOpenTelemetry(options =>
            {
                options.IncludeFormattedMessage = true;
                options.ParseStateValues = true;
                options.AddProcessor(new PrivacyPreservingLogProcessor());
                options.AddProcessor(new SimpleLogRecordExportProcessor(exporter));
            }));
        ILogger logger = loggerFactory.CreateLogger("BunkFy.Tests.Privacy");

        logger.LogError(
            new InvalidOperationException(exceptionCanary),
            "Reservation {ReservationId} failed in {Module} with {ExceptionType}.",
            reservationIdentifierCanary,
            "reservations",
            "InvalidOperationException");

        CapturedLogRecord captured = Assert.Single(exporter.Records);
        Assert.Equal(
            "Reservation {ReservationId} failed in {Module} with {ExceptionType}.",
            captured.Body);
        Assert.Null(captured.FormattedMessage);
        Assert.False(captured.HasException);
        Assert.DoesNotContain(captured.Attributes, attribute => attribute.Key == "ReservationId");
        Assert.Contains(captured.Attributes, attribute =>
            attribute.Key == "Module" && Equals(attribute.Value, "reservations"));
        Assert.Contains(captured.Attributes, attribute =>
            attribute.Key == "ExceptionType" && Equals(attribute.Value, "InvalidOperationException"));
        Assert.DoesNotContain(
            captured.Attributes,
            attribute => attribute.Value?.ToString()?.Contains(
                reservationIdentifierCanary,
                StringComparison.Ordinal) == true);
        Assert.DoesNotContain(exceptionCanary, captured.Body, StringComparison.Ordinal);
    }

    private sealed class CapturingLogExporter : BaseExporter<LogRecord>
    {
        public List<CapturedLogRecord> Records { get; } = [];

        public override ExportResult Export(in Batch<LogRecord> batch)
        {
            foreach (LogRecord record in batch)
            {
                this.Records.Add(new CapturedLogRecord(
                    record.Body,
                    record.FormattedMessage,
                    record.Exception is not null,
                    record.Attributes?.ToArray() ?? []));
            }

            return ExportResult.Success;
        }
    }

    private sealed record CapturedLogRecord(
        string? Body,
        string? FormattedMessage,
        bool HasException,
        IReadOnlyList<KeyValuePair<string, object?>> Attributes);
}
