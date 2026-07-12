namespace BunkFy.Adapters.Tests;

using System.Text;
using BunkFy.Adapter.Abstractions;
using BunkFy.ObservationParsing;
using BunkFy.Parsers.ReservationMail;
using Microsoft.Extensions.DependencyInjection;
using MimeKit;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ReservationMailObservationParserTests
{
    private static readonly DateTimeOffset ObservedAt = new(2026, 7, 12, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Parser_recovers_strict_reservation_envelope_from_one_json_attachment()
    {
        byte[] message = CreateMessage([
            ("provider-booking.json", """
            {
              "schemaVersion": 1,
              "externalRecordId": "booking-42",
              "sourceRevision": "7",
              "sourceUpdatedAtUtc": "2026-07-12T07:30:00Z",
              "payload": { "operation": "upsert", "sourceSequence": 7 }
            }
            """)
        ]);
        IObservationParser parser = CreateParser();
        using ObservationParserInput input = Input(message);

        using ObservationParserResult result = await parser.ParseAsync(input, CancellationToken.None);

        Assert.Equal(ObservationParserDisposition.Parsed, result.Disposition);
        ParsedObservation output = Assert.Single(result.Outputs);
        Assert.Equal("reservation.v1", output.RecordType);
        Assert.Equal("booking-42", output.ExternalRecordId);
        Assert.Equal("7", output.SourceRevision);
        Assert.Equal("application/json", output.ContentType);
        Assert.Contains("sourceSequence", Encoding.UTF8.GetString(output.Payload.Span));
    }

    [Fact]
    public async Task Parser_returns_stable_no_match_for_ambiguous_or_schema_drifted_mail()
    {
        IObservationParser parser = CreateParser();
        byte[] ambiguous = CreateMessage([
            ("one.json", ValidEnvelope("one")),
            ("two.json", ValidEnvelope("two"))
        ]);
        using ObservationParserInput ambiguousInput = Input(ambiguous);
        using ObservationParserResult ambiguousResult = await parser.ParseAsync(
            ambiguousInput,
            CancellationToken.None);

        byte[] drifted = CreateMessage([("one.json", ValidEnvelope("one").Replace(
            "\"payload\"",
            "\"unknown\": true, \"payload\"",
            StringComparison.Ordinal))]);
        using ObservationParserInput driftedInput = Input(drifted);
        using ObservationParserResult driftedResult = await parser.ParseAsync(
            driftedInput,
            CancellationToken.None);

        Assert.Equal(ObservationParserDisposition.NoMatch, ambiguousResult.Disposition);
        Assert.Equal("mail.reservation-json.no-match", ambiguousResult.ReasonCode);
        Assert.Equal(ObservationParserDisposition.NoMatch, driftedResult.Disposition);
    }

    [Theory]
    [InlineData("json.file-drop", "mail.unparsed.v1")]
    [InlineData("imap.reservation-json", "mail.untrusted.v1")]
    public async Task Parser_rejects_undeclared_or_untrusted_source_without_parsing_content(
        string adapterType,
        string sourceRecordType)
    {
        IObservationParser parser = CreateParser();
        byte[] message = CreateMessage([("reservation.json", ValidEnvelope("one"))]);
        using ObservationParserInput input = new(
            Guid.NewGuid(),
            adapterType,
            sourceRecordType,
            "mail-1",
            "1",
            null,
            ObservedAt,
            "message/rfc822",
            message,
            AdapterPayloadHash.ComputeSha256(message));

        using ObservationParserResult result = await parser.ParseAsync(input, CancellationToken.None);

        Assert.Equal(ObservationParserDisposition.NoMatch, result.Disposition);
        Assert.Equal("mail.reservation-json.unsupported-source", result.ReasonCode);
    }

    [Fact]
    public void Descriptor_only_and_executable_registration_are_separate()
    {
        ServiceCollection descriptors = new();
        descriptors.AddReservationMailParserDescriptor();
        Assert.Single(descriptors.BuildServiceProvider().GetServices<IObservationParserDescriptorProvider>());
        Assert.Empty(descriptors.BuildServiceProvider().GetServices<IObservationParser>());

        ServiceCollection executable = new();
        executable.AddReservationMailParser();
        Assert.Single(executable.BuildServiceProvider().GetServices<IObservationParser>());
    }

    private static IObservationParser CreateParser()
    {
        ServiceCollection services = new();
        services.AddReservationMailParser();
        return services.BuildServiceProvider().GetRequiredService<IObservationParser>();
    }

    private static ObservationParserInput Input(byte[] message) => new(
        Guid.NewGuid(),
        "imap.reservation-json",
        "mail.unparsed.v1",
        "mailbox:42:7",
        "7",
        null,
        ObservedAt,
        "message/rfc822",
        message,
        AdapterPayloadHash.ComputeSha256(message));

    private static byte[] CreateMessage(IReadOnlyCollection<(string FileName, string Json)> attachments)
    {
        MimeMessage message = new();
        message.From.Add(MailboxAddress.Parse("provider@example.test"));
        message.To.Add(MailboxAddress.Parse("reservations@example.test"));
        message.Subject = "Reservation update";
        BodyBuilder body = new() { TextBody = "Automated reservation message." };
        foreach ((string fileName, string json) in attachments)
        {
            body.Attachments.Add(fileName, Encoding.UTF8.GetBytes(json), ContentType.Parse("application/json"));
        }

        message.Body = body.ToMessageBody();
        using MemoryStream stream = new();
        message.WriteTo(stream);
        return stream.ToArray();
    }

    private static string ValidEnvelope(string id) => $$"""
        {
          "schemaVersion": 1,
          "externalRecordId": "{{id}}",
          "sourceRevision": "1",
          "payload": { "operation": "upsert", "sourceSequence": 1 }
        }
        """;
}
