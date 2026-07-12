namespace Integration.Tests.Adapters;

using System.Text;
using BunkFy.Adapter.Abstractions;
using BunkFy.Adapters.ImapReservationMail;
using BunkFy.Parsers.ReservationMail;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.DependencyInjection;
using MimeKit;
using Xunit;

public sealed class ImapReservationMailIntegrationTests
{
    private const string Username = "adapter";
    private const string Password = "private";
    private const string SigningKeyId = "2026-q3";
    private const string PreviousSigningKeyId = "2026-q2";
    private const ushort SmtpPort = 3025;
    private const ushort ImapPort = 3143;
    private static readonly byte[] SigningKey = Encoding.ASCII.GetBytes(
        "0123456789abcdef0123456789abcdef");
    private static readonly byte[] PreviousSigningKey = Encoding.ASCII.GetBytes(
        "fedcba9876543210fedcba9876543210");

    [Fact]
    [Trait("Category", "Docker")]
    public async Task MailKit_runner_reads_real_imap_mail_and_checkpoints_valid_and_poison_messages()
    {
        await using IContainer greenMail = new ContainerBuilder("greenmail/standalone:2.1.9")
            .WithEnvironment(
                "GREENMAIL_OPTS",
                "-Dgreenmail.setup.test.smtp -Dgreenmail.setup.test.imap " +
                "-Dgreenmail.hostname=0.0.0.0 -Dgreenmail.users=adapter:private@localhost")
            .WithPortBinding(SmtpPort, assignRandomHostPort: true)
            .WithPortBinding(ImapPort, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilInternalTcpPortIsAvailable(SmtpPort)
                .UntilInternalTcpPortIsAvailable(ImapPort))
            .Build();
        await greenMail.StartAsync();

        string reservationEnvelope = /*lang=json,strict*/ """
            {
              "schemaVersion": 1,
              "externalRecordId": "mail-booking-1",
              "sourceRevision": "1",
              "sourceUpdatedAtUtc": "2026-07-12T12:00:00Z",
              "payload": {
                "operation": "upsert",
                "sourceSequence": 1,
                "arrival": "2026-08-01",
                "departure": "2026-08-03",
                "inventoryUnitIds": ["50000000-0000-0000-0000-000000000001"],
                "primaryGuestName": "Mail Guest",
                "guestCount": 1
              }
            }
            """;
        await SendAsync(
            greenMail.GetMappedPublicPort(SmtpPort),
            CreateMessage("Reservation", reservationEnvelope));

        ServiceCollection services = new();
        services.AddImapReservationMailAdapter();
        await using ServiceProvider provider = services.BuildServiceProvider();
        IAdapterRunner runner = Assert.Single(provider.GetServices<IAdapterRunner>());
        RecordingSink sink = new();
        AdapterRunCompletion first = await RunUntilObservedAsync(
            runner,
            sink,
            checkpoint: null,
            greenMail.GetMappedPublicPort(ImapPort));

        AdapterObservedRecord reservation = Assert.Single(sink.Records);
        Assert.Equal(AdapterRunOutcome.Succeeded, first.Outcome);
        Assert.Equal("reservation.v1", reservation.RecordType);
        Assert.Equal("mail-booking-1", reservation.ExternalRecordId);
        Assert.NotNull(first.AcceptedCheckpoint);

        sink.Records.Clear();
        using AdapterConfigurationMaterial replayMaterial = CreateMaterial(
            greenMail.GetMappedPublicPort(ImapPort));
        AdapterRunCompletion replay = await runner.RunAsync(
            CreateAssignment(first.AcceptedCheckpoint),
            replayMaterial,
            sink,
            CancellationToken.None);
        Assert.Equal(AdapterRunOutcome.Succeeded, replay.Outcome);
        Assert.Equal(0, replay.ObservedCount);
        Assert.Equal(first.AcceptedCheckpoint, replay.AcceptedCheckpoint);
        Assert.Empty(sink.Records);

        string previousEnvelope = reservationEnvelope.Replace(
            "mail-booking-1",
            "mail-booking-previous",
            StringComparison.Ordinal);
        await SendAsync(
            greenMail.GetMappedPublicPort(SmtpPort),
            CreateMessage(
                "Previous producer",
                previousEnvelope,
                PreviousSigningKeyId,
                PreviousSigningKey));
        AdapterRunCompletion overlap = await RunUntilObservedAsync(
            runner,
            sink,
            replay.AcceptedCheckpoint,
            greenMail.GetMappedPublicPort(ImapPort));
        Assert.Equal(AdapterRunOutcome.Succeeded, overlap.Outcome);
        Assert.Equal("mail-booking-previous", Assert.Single(sink.Records).ExternalRecordId);

        sink.Records.Clear();
        string removedEnvelope = reservationEnvelope.Replace(
            "mail-booking-1",
            "mail-booking-removed",
            StringComparison.Ordinal);
        await SendAsync(
            greenMail.GetMappedPublicPort(SmtpPort),
            CreateMessage(
                "Removed producer",
                removedEnvelope,
                PreviousSigningKeyId,
                PreviousSigningKey));
        AdapterRunCompletion removed = await RunUntilObservedAsync(
            runner,
            sink,
            overlap.AcceptedCheckpoint,
            greenMail.GetMappedPublicPort(ImapPort),
            includePreviousKey: false);
        Assert.Equal(AdapterRunOutcome.PartiallySucceeded, removed.Outcome);
        Assert.Equal("mail.untrusted.v1", Assert.Single(sink.Records).RecordType);

        sink.Records.Clear();
        await SendAsync(
            greenMail.GetMappedPublicPort(SmtpPort),
            CreateMessage("Unsupported", attachmentJson: null));
        AdapterRunCompletion poison = await RunUntilObservedAsync(
            runner,
            sink,
            removed.AcceptedCheckpoint,
            greenMail.GetMappedPublicPort(ImapPort));

        AdapterObservedRecord unsupported = Assert.Single(sink.Records);
        Assert.Equal(AdapterRunOutcome.PartiallySucceeded, poison.Outcome);
        Assert.Equal("imap.unsupported-message", poison.ErrorCode);
        Assert.Equal("mail.untrusted.v1", unsupported.RecordType);
        Assert.Equal("message/rfc822", unsupported.ContentType);
        Assert.NotEqual(removed.AcceptedCheckpoint, poison.AcceptedCheckpoint);
    }

    private static async Task SendAsync(ushort port, MimeMessage message)
    {
        using SmtpClient client = new();
        await client.ConnectAsync("127.0.0.1", port, SecureSocketOptions.None);
        await client.AuthenticateAsync(Username, Password);
        await client.SendAsync(message);
        await client.DisconnectAsync(quit: true);
    }

    private static async Task<AdapterRunCompletion> RunUntilObservedAsync(
        IAdapterRunner runner,
        RecordingSink sink,
        string? checkpoint,
        ushort imapPort,
        bool includePreviousKey = true)
    {
        AdapterRunCompletion? completion = null;
        for (int attempt = 0; attempt < 30; attempt++)
        {
            using AdapterConfigurationMaterial material = CreateMaterial(imapPort, includePreviousKey);
            completion = await runner.RunAsync(
                CreateAssignment(checkpoint),
                material,
                sink,
                CancellationToken.None);
            if (completion.ObservedCount > 0)
            {
                return completion;
            }

            await Task.Delay(100);
        }

        return Assert.IsType<AdapterRunCompletion>(completion);
    }

    private static MimeMessage CreateMessage(
        string subject,
        string? attachmentJson,
        string signingKeyId = SigningKeyId,
        byte[]? signingKey = null)
    {
        MimeMessage message = new();
        message.From.Add(MailboxAddress.Parse("provider@example.test"));
        message.To.Add(MailboxAddress.Parse("adapter@localhost"));
        message.Subject = subject;
        Multipart mixed = new("mixed")
        {
            new TextPart("plain") { Text = "Provider message." }
        };
        if (attachmentJson is not null)
        {
            byte[] attachmentBytes = Encoding.UTF8.GetBytes(attachmentJson);
            message.Headers.Add(
                ReservationMailAttachmentSignature.HeaderName,
                ReservationMailAttachmentSignature.Create(
                    signingKeyId,
                    signingKey ?? SigningKey,
                    attachmentBytes));
            mixed.Add(new MimePart("application", "json")
            {
                FileName = "reservation.json",
                Content = new MimeContent(new MemoryStream(attachmentBytes, writable: false)),
                ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                ContentTransferEncoding = ContentEncoding.Base64
            });
        }

        message.Body = mixed;
        return message;
    }

    private static AdapterConfigurationMaterial CreateMaterial(
        ushort imapPort,
        bool includePreviousKey = true)
    {
        string configuration = $$"""
            {
              "host": "127.0.0.1",
              "port": {{imapPort}},
              "mailbox": "INBOX",
              "attachmentFileName": "reservation.json",
              "transportSecurity": "none",
              "allowInsecureLoopback": true,
              "networkTimeoutSeconds": 30,
              "maximumMessagesPerRun": 10,
              "maximumMessageBytes": 4194304,
              "maximumAttachmentBytes": 1048576
            }
            """;
        string previousKey = includePreviousKey
            ? $$""",{"keyId":"{{PreviousSigningKeyId}}","key":"{{Convert.ToBase64String(PreviousSigningKey)}}"}"""
            : string.Empty;
        string secret = $$"""
            {
              "authentication": "password",
              "username": "{{Username}}",
              "credential": "{{Password}}",
              "observationSigningKeys": [
                {"keyId":"{{SigningKeyId}}","key":"{{Convert.ToBase64String(SigningKey)}}"}{{previousKey}}
              ]
            }
            """;
        return new AdapterConfigurationMaterial(
            3,
            "application/json",
            Encoding.UTF8.GetBytes(configuration),
            "application/json",
            Encoding.UTF8.GetBytes(secret));
    }

    private static AdapterRunAssignment CreateAssignment(string? checkpoint)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return new AdapterRunAssignment(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.Parse("30000000-0000-0000-0000-000000000001"),
            "tenant-a",
            Guid.Parse("40000000-0000-0000-0000-000000000001"),
            ImapReservationMailAdapterDescriptor.AdapterType,
            AdapterExecutionMode.Polling,
            now,
            now.AddMinutes(5),
            checkpoint);
    }

    private sealed class RecordingSink : IAdapterObservationSink
    {
        public List<AdapterObservedRecord> Records { get; } = [];

        public Task<AdapterObservationAcknowledgement> SubmitAsync(
            AdapterObservationSubmission submission,
            CancellationToken cancellationToken)
        {
            this.Records.AddRange(submission.Records);
            return Task.FromResult(new AdapterObservationAcknowledgement(
                submission.RunId,
                submission.LeaseId,
                submission.Records.Select(record => new AdapterObservationResult(
                    record.OperationId,
                    AdapterObservationDisposition.Accepted,
                    Guid.NewGuid(),
                    errorCode: null)).ToArray(),
                checkpointAccepted: true,
                submission.ProposedCheckpoint));
        }
    }
}
