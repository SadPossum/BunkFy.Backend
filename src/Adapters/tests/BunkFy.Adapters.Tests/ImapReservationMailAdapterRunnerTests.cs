namespace BunkFy.Adapters.Tests;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BunkFy.Adapter.Abstractions;
using BunkFy.Adapters.ImapReservationMail;
using BunkFy.Parsers.ReservationMail;
using Microsoft.Extensions.DependencyInjection;
using MimeKit;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ImapReservationMailAdapterRunnerTests
{
    private const string SigningKeyId = "2026-q3";
    private const string PreviousSigningKeyId = "2026-q2";
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);
    private static readonly byte[] SigningKey = Encoding.ASCII.GetBytes(
        "0123456789abcdef0123456789abcdef");
    private static readonly byte[] PreviousSigningKey = Encoding.ASCII.GetBytes(
        "fedcba9876543210fedcba9876543210");

    [Fact]
    public async Task Reads_strict_reservation_attachment_and_advances_uid_checkpoint_after_acknowledgement()
    {
        byte[] message = CreateMessage(ValidEnvelope);
        FakeMailboxClientFactory factory = new(
            42,
            [new(7, message.Length, Now)],
            new Dictionary<uint, byte[]> { [7] = message });
        ImapReservationMailAdapterRunner runner = new(factory, new FixedTimeProvider(Now));
        RecordingSink sink = new();
        using AdapterConfigurationMaterial material = CreateMaterial();

        AdapterRunCompletion completion = await runner.RunAsync(
            CreateAssignment(checkpoint: null),
            material,
            sink,
            CancellationToken.None);

        AdapterObservedRecord record = Assert.Single(sink.Records);
        Assert.Equal(AdapterRunOutcome.Succeeded, completion.Outcome);
        Assert.Equal("reservation.v1", record.RecordType);
        Assert.Equal("booking-42", record.ExternalRecordId);
        Assert.Equal("2", record.SourceRevision);
        using JsonDocument payload = JsonDocument.Parse(record.Payload);
        Assert.Equal(2, payload.RootElement.GetProperty("sourceSequence").GetInt64());
        ImapReservationMailCheckpoint checkpoint = Assert.IsType<ImapReservationMailCheckpoint>(
            ImapReservationMailCheckpoint.Parse(completion.AcceptedCheckpoint));
        Assert.Equal(42U, checkpoint.UidValidity);
        Assert.Equal(7U, checkpoint.LastUid);
        Assert.Equal(AdapterProtocolLimits.Sha256Length, checkpoint.MailboxKey.Length);
        Assert.Equal(ImapAuthenticationKind.Password, factory.Credential!.Authentication);
        Assert.Equal("adapter@example.test", factory.Credential.Username);
        Assert.All(
            factory.Credential.ObservationSigningKeys,
            signingKey => Assert.All(signingKey.Key, value => Assert.Equal(0, value)));
    }

    [Fact]
    public async Task Stable_mailbox_identity_replays_the_same_operation_and_checkpoint()
    {
        byte[] message = CreateMessage(ValidEnvelope);
        FakeMailboxClientFactory factory = new(
            42,
            [new(7, message.Length, Now)],
            new Dictionary<uint, byte[]> { [7] = message });
        ImapReservationMailAdapterRunner runner = new(factory, new FixedTimeProvider(Now));
        using AdapterConfigurationMaterial firstMaterial = CreateMaterial();
        using AdapterConfigurationMaterial secondMaterial = CreateMaterial();
        RecordingSink firstSink = new();
        RecordingSink secondSink = new(AdapterObservationDisposition.Duplicate);

        AdapterRunCompletion first = await runner.RunAsync(
            CreateAssignment(checkpoint: null), firstMaterial, firstSink, CancellationToken.None);
        AdapterRunCompletion second = await runner.RunAsync(
            CreateAssignment(checkpoint: null), secondMaterial, secondSink, CancellationToken.None);

        Assert.Equal(Assert.Single(firstSink.Records).OperationId, Assert.Single(secondSink.Records).OperationId);
        Assert.Equal(first.AcceptedCheckpoint, second.AcceptedCheckpoint);
        Assert.Equal(AdapterRunOutcome.Succeeded, second.Outcome);
    }

    [Fact]
    public async Task Malformed_and_oversized_messages_become_durable_evidence_without_blocking_later_uid()
    {
        byte[] malformed = "not-a-mime-message"u8.ToArray();
        FakeMailboxClientFactory factory = new(
            9,
            [new(3, malformed.Length, Now), new(8, 5 * 1024 * 1024, Now.AddMinutes(1))],
            new Dictionary<uint, byte[]> { [3] = malformed });
        ImapReservationMailAdapterRunner runner = new(factory, new FixedTimeProvider(Now));
        RecordingSink sink = new();
        using AdapterConfigurationMaterial material = CreateMaterial();

        AdapterRunCompletion completion = await runner.RunAsync(
            CreateAssignment(checkpoint: null), material, sink, CancellationToken.None);

        Assert.Equal(AdapterRunOutcome.PartiallySucceeded, completion.Outcome);
        Assert.Equal("imap.unsupported-message", completion.ErrorCode);
        Assert.Collection(
            sink.Records,
            record =>
            {
                Assert.Equal("mail.untrusted.v1", record.RecordType);
                Assert.Equal("message/rfc822", record.ContentType);
                Assert.Equal(malformed, record.Payload.ToArray());
            },
            record =>
            {
                Assert.Equal("mail.oversized.v1", record.RecordType);
                Assert.Equal("application/json", record.ContentType);
                Assert.DoesNotContain("not-a-mime-message", Encoding.UTF8.GetString(record.Payload.Span), StringComparison.Ordinal);
            });
        Assert.Equal([3U], factory.ReadUids);
        Assert.Equal(8U, ImapReservationMailCheckpoint.Parse(completion.AcceptedCheckpoint)!.Value.LastUid);
    }

    [Fact]
    public async Task Changed_uid_validity_fails_closed_before_message_listing_or_submission()
    {
        FakeMailboxClientFactory factory = new(43, [], new Dictionary<uint, byte[]>());
        ImapReservationMailAdapterRunner runner = new(factory, new FixedTimeProvider(Now));
        RecordingSink sink = new();
        using AdapterConfigurationMaterial material = CreateMaterial();
        (ImapReservationMailSettings settings, ImapCredential parsedCredential) =
            ImapReservationMailMaterial.Parse(material);
        using ImapCredential credential = parsedCredential;
        string checkpoint = new ImapReservationMailCheckpoint(
            ImapReservationMailCheckpoint.CreateMailboxKey(settings, credential),
            42,
            7).Serialize();

        AdapterRunCompletion completion = await runner.RunAsync(
            CreateAssignment(checkpoint), material, sink, CancellationToken.None);

        Assert.Equal(AdapterRunOutcome.Failed, completion.Outcome);
        Assert.Equal("imap.uid-validity-changed", completion.ErrorCode);
        Assert.Equal(checkpoint, completion.AcceptedCheckpoint);
        Assert.Equal(0, factory.ListCalls);
        Assert.Empty(sink.Records);
    }

    [Fact]
    public async Task Changed_mailbox_identity_fails_before_connecting_even_when_uid_values_could_overlap()
    {
        using AdapterConfigurationMaterial baselineMaterial = CreateMaterial();
        (ImapReservationMailSettings baseline, ImapCredential parsedCredential) =
            ImapReservationMailMaterial.Parse(baselineMaterial);
        using ImapCredential credential = parsedCredential;
        string checkpoint = new ImapReservationMailCheckpoint(
            ImapReservationMailCheckpoint.CreateMailboxKey(baseline, credential),
            42,
            7).Serialize();
        FakeMailboxClientFactory factory = new(42, [], new Dictionary<uint, byte[]>());
        ImapReservationMailAdapterRunner runner = new(factory, new FixedTimeProvider(Now));
        using AdapterConfigurationMaterial changedMaterial = CreateMaterial(host: "other.example.test");

        AdapterRunCompletion completion = await runner.RunAsync(
            CreateAssignment(checkpoint),
            changedMaterial,
            new RecordingSink(),
            CancellationToken.None);

        Assert.Equal(AdapterRunOutcome.Failed, completion.Outcome);
        Assert.Equal("imap.mailbox-identity-changed", completion.ErrorCode);
        Assert.Equal(0, factory.OpenCalls);
    }

    [Fact]
    public async Task Rejected_observation_does_not_advance_checkpoint()
    {
        byte[] message = CreateMessage(ValidEnvelope);
        FakeMailboxClientFactory factory = new(
            42,
            [new(7, message.Length, Now)],
            new Dictionary<uint, byte[]> { [7] = message });
        ImapReservationMailAdapterRunner runner = new(factory, new FixedTimeProvider(Now));
        RecordingSink sink = new(AdapterObservationDisposition.Rejected);
        using AdapterConfigurationMaterial material = CreateMaterial();

        AdapterRunCompletion completion = await runner.RunAsync(
            CreateAssignment(checkpoint: null), material, sink, CancellationToken.None);

        Assert.Equal(AdapterRunOutcome.PartiallySucceeded, completion.Outcome);
        Assert.Equal("imap.observation-rejected", completion.ErrorCode);
        Assert.Null(completion.AcceptedCheckpoint);
    }

    [Fact]
    public async Task Batch_limit_stops_before_later_uid_and_checkpoints_only_submitted_messages()
    {
        byte[] first = CreateMessage(ValidEnvelope.Replace("booking-42", "booking-1", StringComparison.Ordinal));
        byte[] second = CreateMessage(ValidEnvelope.Replace("booking-42", "booking-2", StringComparison.Ordinal));
        byte[] third = CreateMessage(ValidEnvelope.Replace("booking-42", "booking-3", StringComparison.Ordinal));
        FakeMailboxClientFactory factory = new(
            42,
            [new(1, first.Length, Now), new(2, second.Length, Now), new(3, third.Length, Now)],
            new Dictionary<uint, byte[]> { [1] = first, [2] = second, [3] = third });
        ImapReservationMailAdapterRunner runner = new(factory, new FixedTimeProvider(Now));
        RecordingSink sink = new();
        using AdapterConfigurationMaterial material = CreateMaterial(maximumMessagesPerRun: 2);

        AdapterRunCompletion completion = await runner.RunAsync(
            CreateAssignment(checkpoint: null), material, sink, CancellationToken.None);

        Assert.Equal(2, completion.ObservedCount);
        Assert.Equal([1U, 2U], factory.ReadUids);
        Assert.Equal(2U, ImapReservationMailCheckpoint.Parse(completion.AcceptedCheckpoint)!.Value.LastUid);
        Assert.Equal(["booking-1", "booking-2"], sink.Records.Select(record => record.ExternalRecordId));
    }

    [Fact]
    public async Task Acknowledgement_identity_mismatch_fails_without_checkpoint_advancement()
    {
        byte[] message = CreateMessage(ValidEnvelope);
        FakeMailboxClientFactory factory = new(
            42,
            [new(7, message.Length, Now)],
            new Dictionary<uint, byte[]> { [7] = message });
        ImapReservationMailAdapterRunner runner = new(factory, new FixedTimeProvider(Now));
        using AdapterConfigurationMaterial material = CreateMaterial();

        AdapterRunCompletion completion = await runner.RunAsync(
            CreateAssignment(checkpoint: null),
            material,
            new MismatchedSink(),
            CancellationToken.None);

        Assert.Equal(AdapterRunOutcome.Failed, completion.Outcome);
        Assert.Equal("imap.acknowledgement-mismatch", completion.ErrorCode);
        Assert.Null(completion.AcceptedCheckpoint);
    }

    [Fact]
    public async Task Ambiguous_matching_attachments_are_retained_as_untrusted_mail()
    {
        byte[] message = CreateMessage(ValidEnvelope, duplicateAttachment: true);
        FakeMailboxClientFactory factory = new(
            42,
            [new(7, message.Length, Now)],
            new Dictionary<uint, byte[]> { [7] = message });
        ImapReservationMailAdapterRunner runner = new(factory, new FixedTimeProvider(Now));
        RecordingSink sink = new();
        using AdapterConfigurationMaterial material = CreateMaterial();

        AdapterRunCompletion completion = await runner.RunAsync(
            CreateAssignment(checkpoint: null), material, sink, CancellationToken.None);

        Assert.Equal(AdapterRunOutcome.PartiallySucceeded, completion.Outcome);
        Assert.Equal("mail.untrusted.v1", Assert.Single(sink.Records).RecordType);
    }

    [Fact]
    public async Task Authenticated_malformed_envelope_is_retained_as_replayable_unparsed_mail()
    {
        byte[] message = CreateMessage("""{"schemaVersion":2}""");
        FakeMailboxClientFactory factory = new(
            42,
            [new(7, message.Length, Now)],
            new Dictionary<uint, byte[]> { [7] = message });
        ImapReservationMailAdapterRunner runner = new(factory, new FixedTimeProvider(Now));
        RecordingSink sink = new();
        using AdapterConfigurationMaterial material = CreateMaterial();

        AdapterRunCompletion completion = await runner.RunAsync(
            CreateAssignment(checkpoint: null), material, sink, CancellationToken.None);

        Assert.Equal(AdapterRunOutcome.PartiallySucceeded, completion.Outcome);
        Assert.Equal("mail.unparsed.v1", Assert.Single(sink.Records).RecordType);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task Missing_or_duplicate_signature_is_retained_as_untrusted_mail(
        bool includeSignature,
        bool duplicateSignature)
    {
        byte[] message = CreateMessage(
            ValidEnvelope,
            includeSignature: includeSignature,
            duplicateSignature: duplicateSignature);
        FakeMailboxClientFactory factory = new(
            42,
            [new(7, message.Length, Now)],
            new Dictionary<uint, byte[]> { [7] = message });
        ImapReservationMailAdapterRunner runner = new(factory, new FixedTimeProvider(Now));
        RecordingSink sink = new();
        using AdapterConfigurationMaterial material = CreateMaterial();

        AdapterRunCompletion completion = await runner.RunAsync(
            CreateAssignment(checkpoint: null), material, sink, CancellationToken.None);

        Assert.Equal(AdapterRunOutcome.PartiallySucceeded, completion.Outcome);
        Assert.Equal("mail.untrusted.v1", Assert.Single(sink.Records).RecordType);
    }

    [Fact]
    public async Task Attachment_tampering_after_signing_is_retained_as_untrusted_mail()
    {
        byte[] message = CreateMessage(
            ValidEnvelope.Replace("booking-42", "booking-tampered", StringComparison.Ordinal),
            signatureEnvelope: ValidEnvelope);
        FakeMailboxClientFactory factory = new(
            42,
            [new(7, message.Length, Now)],
            new Dictionary<uint, byte[]> { [7] = message });
        ImapReservationMailAdapterRunner runner = new(factory, new FixedTimeProvider(Now));
        RecordingSink sink = new();
        using AdapterConfigurationMaterial material = CreateMaterial();

        await runner.RunAsync(CreateAssignment(checkpoint: null), material, sink, CancellationToken.None);

        Assert.Equal("mail.untrusted.v1", Assert.Single(sink.Records).RecordType);
    }

    [Fact]
    public async Task Malformed_signature_value_is_retained_as_untrusted_mail()
    {
        byte[] message = CreateMessage(ValidEnvelope, signatureOverride: "v1=not-base64");
        FakeMailboxClientFactory factory = new(
            42,
            [new(7, message.Length, Now)],
            new Dictionary<uint, byte[]> { [7] = message });
        ImapReservationMailAdapterRunner runner = new(factory, new FixedTimeProvider(Now));
        RecordingSink sink = new();
        using AdapterConfigurationMaterial material = CreateMaterial();

        await runner.RunAsync(CreateAssignment(checkpoint: null), material, sink, CancellationToken.None);

        Assert.Equal("mail.untrusted.v1", Assert.Single(sink.Records).RecordType);
    }

    [Fact]
    public async Task Overlapping_key_ring_accepts_messages_from_current_and_previous_producers()
    {
        byte[] current = CreateMessage(ValidEnvelope);
        byte[] previous = CreateMessage(
            ValidEnvelope.Replace("booking-42", "booking-previous", StringComparison.Ordinal),
            signingKeyId: PreviousSigningKeyId,
            signingKey: PreviousSigningKey);
        FakeMailboxClientFactory factory = new(
            42,
            [new(7, current.Length, Now), new(8, previous.Length, Now.AddMinutes(1))],
            new Dictionary<uint, byte[]> { [7] = current, [8] = previous });
        ImapReservationMailAdapterRunner runner = new(factory, new FixedTimeProvider(Now));
        RecordingSink sink = new();
        using AdapterConfigurationMaterial material = CreateMaterial(
            secret: CreateSecret((SigningKeyId, SigningKey), (PreviousSigningKeyId, PreviousSigningKey)));

        AdapterRunCompletion completion = await runner.RunAsync(
            CreateAssignment(checkpoint: null), material, sink, CancellationToken.None);

        Assert.Equal(AdapterRunOutcome.Succeeded, completion.Outcome);
        Assert.Equal(["booking-42", "booking-previous"], sink.Records.Select(record => record.ExternalRecordId));
    }

    [Fact]
    public async Task Removed_key_id_is_retained_as_untrusted_without_trying_other_keys()
    {
        byte[] message = CreateMessage(
            ValidEnvelope,
            signingKeyId: PreviousSigningKeyId,
            signingKey: PreviousSigningKey);
        FakeMailboxClientFactory factory = new(
            42,
            [new(7, message.Length, Now)],
            new Dictionary<uint, byte[]> { [7] = message });
        ImapReservationMailAdapterRunner runner = new(factory, new FixedTimeProvider(Now));
        RecordingSink sink = new();
        using AdapterConfigurationMaterial material = CreateMaterial();

        await runner.RunAsync(CreateAssignment(checkpoint: null), material, sink, CancellationToken.None);

        Assert.Equal("mail.untrusted.v1", Assert.Single(sink.Records).RecordType);
    }

    [Fact]
    public async Task Relabeling_a_signature_to_another_configured_key_id_invalidates_it()
    {
        string previousSignature = ReservationMailAttachmentSignature.Create(
            PreviousSigningKeyId,
            PreviousSigningKey,
            Encoding.UTF8.GetBytes(ValidEnvelope));
        byte[] message = CreateMessage(
            ValidEnvelope,
            signatureOverride: previousSignature.Replace(
                PreviousSigningKeyId,
                SigningKeyId,
                StringComparison.Ordinal));
        FakeMailboxClientFactory factory = new(
            42,
            [new(7, message.Length, Now)],
            new Dictionary<uint, byte[]> { [7] = message });
        ImapReservationMailAdapterRunner runner = new(factory, new FixedTimeProvider(Now));
        RecordingSink sink = new();
        using AdapterConfigurationMaterial material = CreateMaterial(
            secret: CreateSecret((SigningKeyId, SigningKey), (PreviousSigningKeyId, PreviousSigningKey)));

        await runner.RunAsync(CreateAssignment(checkpoint: null), material, sink, CancellationToken.None);

        Assert.Equal("mail.untrusted.v1", Assert.Single(sink.Records).RecordType);
    }

    [Fact]
    public async Task Attachment_decode_bound_fails_to_untrusted_evidence_without_blocking_uid()
    {
        string largeEnvelope = JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            externalRecordId = "large",
            sourceRevision = "1",
            payload = new { value = new string('x', 2048) }
        });
        byte[] message = CreateMessage(largeEnvelope);
        FakeMailboxClientFactory factory = new(
            42,
            [new(7, message.Length, Now)],
            new Dictionary<uint, byte[]> { [7] = message });
        ImapReservationMailAdapterRunner runner = new(factory, new FixedTimeProvider(Now));
        RecordingSink sink = new();
        using AdapterConfigurationMaterial material = CreateMaterial(maximumAttachmentBytes: 1024);

        AdapterRunCompletion completion = await runner.RunAsync(
            CreateAssignment(checkpoint: null), material, sink, CancellationToken.None);

        Assert.Equal("mail.untrusted.v1", Assert.Single(sink.Records).RecordType);
        Assert.Equal(7U, ImapReservationMailCheckpoint.Parse(completion.AcceptedCheckpoint)!.Value.LastUid);
    }

    [Fact]
    public async Task Authenticated_oversized_source_identity_is_unparsed_instead_of_blocking_mailbox()
    {
        string envelope = ValidEnvelope.Replace(
            "booking-42",
            new string('x', AdapterProtocolLimits.ExternalRecordIdMaxLength + 1),
            StringComparison.Ordinal);
        byte[] message = CreateMessage(envelope);
        FakeMailboxClientFactory factory = new(
            42,
            [new(7, message.Length, Now)],
            new Dictionary<uint, byte[]> { [7] = message });
        ImapReservationMailAdapterRunner runner = new(factory, new FixedTimeProvider(Now));
        RecordingSink sink = new();
        using AdapterConfigurationMaterial material = CreateMaterial();

        await runner.RunAsync(CreateAssignment(checkpoint: null), material, sink, CancellationToken.None);

        Assert.Equal("mail.unparsed.v1", Assert.Single(sink.Records).RecordType);
    }

    [Theory]
    [InlineData("imap.example.test", "none", true)]
    [InlineData("127.0.0.1", "none", false)]
    [InlineData("127.0.0.1", "auto", true)]
    public void Material_rejects_insecure_or_implicit_transport(string host, string security, bool allowInsecure)
    {
        using AdapterConfigurationMaterial material = CreateMaterial(host, security, allowInsecure);

        Assert.Throws<InvalidOperationException>(() => ImapReservationMailMaterial.Parse(material));
    }

    [Fact]
    public void Material_selects_explicit_oauth_without_exposing_it_in_configuration()
    {
        using AdapterConfigurationMaterial material = CreateMaterial(
            secret: $$"""{"authentication":"oauth2","username":"adapter@example.test","credential":"access-token","observationSigningKeys":[{"keyId":"{{SigningKeyId}}","key":"{{Convert.ToBase64String(SigningKey)}}"}]}""");

        (ImapReservationMailSettings settings, ImapCredential parsedCredential) =
            ImapReservationMailMaterial.Parse(material);
        using ImapCredential credential = parsedCredential;

        Assert.Equal(ImapAuthenticationKind.OAuth2, credential.Authentication);
        Assert.Equal("access-token", credential.Credential);
        Assert.DoesNotContain("access-token", settings.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("access-token", credential.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("adapter@example.test", credential.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(SigningKeyId, credential.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(SigningKeyId, Assert.Single(credential.ObservationSigningKeys).ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Password_credential_is_preserved_exactly()
    {
        using AdapterConfigurationMaterial material = CreateMaterial(
            secret: $$"""{"authentication":"password","username":"adapter@example.test","credential":"  private  ","observationSigningKeys":[{"keyId":"{{SigningKeyId}}","key":"{{Convert.ToBase64String(SigningKey)}}"}]}""");

        (_, ImapCredential parsedCredential) = ImapReservationMailMaterial.Parse(material);
        using ImapCredential credential = parsedCredential;

        Assert.Equal("  private  ", credential.Credential);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(65)]
    public void Material_rejects_short_or_oversized_signing_keys(int keyBytes)
    {
        string secret = JsonSerializer.Serialize(new
        {
            authentication = "password",
            username = "adapter@example.test",
            credential = "private",
            observationSigningKeys = new[]
            {
                new { keyId = SigningKeyId, key = Convert.ToBase64String(new byte[keyBytes]) }
            }
        });
        using AdapterConfigurationMaterial material = CreateMaterial(secret: secret);

        Assert.Throws<InvalidOperationException>(() => ImapReservationMailMaterial.Parse(material));
    }

    [Fact]
    public void Signing_key_rotation_does_not_change_mailbox_checkpoint_identity()
    {
        using AdapterConfigurationMaterial firstMaterial = CreateMaterial();
        string rotatedSecret = CreateSecret((PreviousSigningKeyId, PreviousSigningKey));
        using AdapterConfigurationMaterial secondMaterial = CreateMaterial(secret: rotatedSecret);
        (ImapReservationMailSettings firstSettings, ImapCredential firstParsed) =
            ImapReservationMailMaterial.Parse(firstMaterial);
        (ImapReservationMailSettings secondSettings, ImapCredential secondParsed) =
            ImapReservationMailMaterial.Parse(secondMaterial);
        using ImapCredential first = firstParsed;
        using ImapCredential second = secondParsed;

        Assert.Equal(
            ImapReservationMailCheckpoint.CreateMailboxKey(firstSettings, first),
            ImapReservationMailCheckpoint.CreateMailboxKey(secondSettings, second));
    }

    [Fact]
    public void Material_rejects_duplicate_key_ids_material_null_entries_and_oversized_rings()
    {
        byte[] third = Encoding.ASCII.GetBytes("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        byte[] fourth = Encoding.ASCII.GetBytes("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
        byte[] fifth = Encoding.ASCII.GetBytes("cccccccccccccccccccccccccccccccc");
        string[] invalidSecrets =
        [
            CreateSecret((SigningKeyId, SigningKey), (SigningKeyId, PreviousSigningKey)),
            CreateSecret((SigningKeyId, SigningKey), (PreviousSigningKeyId, SigningKey)),
            JsonSerializer.Serialize(new
            {
                authentication = "password",
                username = "adapter@example.test",
                credential = "private",
                observationSigningKeys = new object?[]
                {
                    new { keyId = SigningKeyId, key = Convert.ToBase64String(SigningKey) },
                    null
                }
            }),
            CreateSecret(
                (SigningKeyId, SigningKey),
                (PreviousSigningKeyId, PreviousSigningKey),
                ("2026-q1", third),
                ("2025-q4", fourth),
                ("2025-q3", fifth))
        ];

        foreach (string secret in invalidSecrets)
        {
            using AdapterConfigurationMaterial material = CreateMaterial(secret: secret);
            Assert.Throws<InvalidOperationException>(() => ImapReservationMailMaterial.Parse(material));
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("UPPER")]
    [InlineData("-starts-with-symbol")]
    [InlineData("contains space")]
    [InlineData("contains:colon")]
    public void Signing_key_id_grammar_is_strict(string keyId)
    {
        Assert.Throws<ArgumentException>(() => ReservationMailAttachmentSignature.ValidateKeyId(keyId));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"schemaVersion\":2,\"mailboxKey\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"uidValidity\":1,\"lastUid\":1}")]
    [InlineData("{\"schemaVersion\":1,\"mailboxKey\":\"not-a-hash\",\"uidValidity\":1,\"lastUid\":1}")]
    public void Checkpoint_rejects_missing_unsupported_or_unbound_state(string checkpoint)
    {
        Assert.Throws<InvalidOperationException>(() => ImapReservationMailCheckpoint.Parse(checkpoint));
    }

    [Fact]
    public void Descriptor_only_registration_does_not_load_executable_runner()
    {
        ServiceCollection descriptors = new();
        descriptors.AddImapReservationMailAdapterDescriptor();
        using ServiceProvider descriptorProvider = descriptors.BuildServiceProvider();
        Assert.Equal(
            ImapReservationMailAdapterDescriptor.AdapterType,
            Assert.Single(descriptorProvider.GetServices<IAdapterDescriptorProvider>()).Descriptor.AdapterType);
        Assert.Equal(3, ImapReservationMailAdapterDescriptor.Value.ConfigurationSchemaVersion);
        Assert.Empty(descriptorProvider.GetServices<IAdapterRunner>());

        ServiceCollection executable = new();
        executable.AddImapReservationMailAdapter();
        using ServiceProvider runnerProvider = executable.BuildServiceProvider();
        Assert.Equal(
            ImapReservationMailAdapterDescriptor.AdapterType,
            Assert.Single(runnerProvider.GetServices<IAdapterRunner>()).Descriptor.AdapterType);
    }

    private static AdapterRunAssignment CreateAssignment(string? checkpoint)
    {
        return new AdapterRunAssignment(
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            Guid.Parse("20000000-0000-0000-0000-000000000001"),
            Guid.Parse("30000000-0000-0000-0000-000000000001"),
            "tenant-a",
            Guid.Parse("40000000-0000-0000-0000-000000000001"),
            ImapReservationMailAdapterDescriptor.AdapterType,
            AdapterExecutionMode.Polling,
            Now,
            Now.AddMinutes(5),
            checkpoint);
    }

    private static AdapterConfigurationMaterial CreateMaterial(
        string host = "imap.example.test",
        string security = "tls",
        bool allowInsecure = false,
        int maximumMessagesPerRun = 25,
        int maximumAttachmentBytes = 1048576,
        string? secret = null)
    {
        string configuration = $$"""
            {
              "host": "{{host}}",
              "port": 993,
              "mailbox": "INBOX",
              "attachmentFileName": "reservation.json",
              "transportSecurity": "{{security}}",
              "allowInsecureLoopback": {{allowInsecure.ToString().ToLowerInvariant()}},
              "networkTimeoutSeconds": 30,
              "maximumMessagesPerRun": {{maximumMessagesPerRun}},
              "maximumMessageBytes": 4194304,
              "maximumAttachmentBytes": {{maximumAttachmentBytes}}
            }
            """;
        return new AdapterConfigurationMaterial(
            3,
            "application/json",
            Encoding.UTF8.GetBytes(configuration),
            "application/json",
            Encoding.UTF8.GetBytes(secret ?? CreateSecret((SigningKeyId, SigningKey))));
    }

    private static byte[] CreateMessage(
        string envelope,
        bool duplicateAttachment = false,
        bool includeSignature = true,
        bool duplicateSignature = false,
        string? signatureEnvelope = null,
        string? signatureOverride = null,
        string signingKeyId = SigningKeyId,
        byte[]? signingKey = null)
    {
        MimeMessage message = new();
        message.From.Add(MailboxAddress.Parse("provider@example.test"));
        message.To.Add(MailboxAddress.Parse("adapter@example.test"));
        message.Subject = "Reservation update";
        Multipart mixed = new("mixed") { new TextPart("plain") { Text = "Attached." } };
        byte[] envelopeBytes = Encoding.UTF8.GetBytes(envelope);
        if (includeSignature)
        {
            byte[] signedBytes = Encoding.UTF8.GetBytes(signatureEnvelope ?? envelope);
            try
            {
                string signature = signatureOverride ??
                    ReservationMailAttachmentSignature.Create(signingKeyId, signingKey ?? SigningKey, signedBytes);
                message.Headers.Add(ReservationMailAttachmentSignature.HeaderName, signature);
                if (duplicateSignature)
                {
                    message.Headers.Add(ReservationMailAttachmentSignature.HeaderName, signature);
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(signedBytes);
            }
        }
        MimePart attachment = new("application", "json")
        {
            FileName = "reservation.json",
            Content = new MimeContent(new MemoryStream(envelopeBytes, writable: false)),
            ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
            ContentTransferEncoding = ContentEncoding.Base64
        };
        mixed.Add(attachment);
        if (duplicateAttachment)
        {
            mixed.Add(new MimePart("application", "json")
            {
                FileName = "reservation.json",
                Content = new MimeContent(new MemoryStream(envelopeBytes, writable: false)),
                ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                ContentTransferEncoding = ContentEncoding.Base64
            });
        }
        message.Body = mixed;
        using MemoryStream destination = new();
        message.WriteTo(destination);
        return destination.ToArray();
    }

    private static string CreateSecret(params (string KeyId, byte[] Key)[] signingKeys) =>
        JsonSerializer.Serialize(new
        {
            authentication = "password",
            username = "adapter@example.test",
            credential = "private",
            observationSigningKeys = signingKeys.Select(signingKey => new
            {
                keyId = signingKey.KeyId,
                key = Convert.ToBase64String(signingKey.Key)
            }).ToArray()
        });

    private const string ValidEnvelope = """
        {
          "schemaVersion": 1,
          "externalRecordId": "booking-42",
          "sourceRevision": "2",
          "sourceUpdatedAtUtc": "2026-07-12T11:59:00Z",
          "payload": {
            "operation": "upsert",
            "sourceSequence": 2,
            "arrival": "2026-08-01",
            "departure": "2026-08-04",
            "inventoryUnitIds": ["50000000-0000-0000-0000-000000000001"],
            "primaryGuestName": "Test Guest",
            "guestCount": 1
          }
        }
        """;

    private sealed class FakeMailboxClientFactory(
        uint uidValidity,
        IReadOnlyCollection<ImapMailboxMessageSummary> summaries,
        IReadOnlyDictionary<uint, byte[]> messages) : IImapMailboxClientFactory
    {
        private readonly uint uidValidity = uidValidity;
        private readonly IReadOnlyCollection<ImapMailboxMessageSummary> summaries = summaries;
        private readonly IReadOnlyDictionary<uint, byte[]> messages = messages;

        public ImapReservationMailSettings? Settings { get; private set; }
        public ImapCredential? Credential { get; private set; }
        public List<uint> ReadUids { get; } = [];
        public int ListCalls { get; private set; }
        public int OpenCalls { get; private set; }

        public Task<IImapMailboxSession> OpenAsync(
            ImapReservationMailSettings settings,
            ImapCredential credential,
            CancellationToken cancellationToken)
        {
            this.OpenCalls++;
            this.Settings = settings;
            this.Credential = credential;
            return Task.FromResult<IImapMailboxSession>(new FakeSession(this));
        }

        private sealed class FakeSession(FakeMailboxClientFactory owner) : IImapMailboxSession
        {
            public uint UidValidity => owner.uidValidity;

            public Task<ImapMailboxMessageSummary?> GetNextAsync(
                uint afterUid,
                CancellationToken cancellationToken)
            {
                owner.ListCalls++;
                return Task.FromResult(owner.summaries
                    .Where(summary => summary.Uid > afterUid)
                    .OrderBy(summary => summary.Uid)
                    .FirstOrDefault());
            }

            public Task<byte[]> ReadMessageAsync(
                uint uid,
                int maximumBytes,
                CancellationToken cancellationToken)
            {
                owner.ReadUids.Add(uid);
                return Task.FromResult(owner.messages[uid].ToArray());
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingSink(
        AdapterObservationDisposition disposition = AdapterObservationDisposition.Accepted)
        : IAdapterObservationSink
    {
        public List<AdapterObservedRecord> Records { get; } = [];

        public Task<AdapterObservationAcknowledgement> SubmitAsync(
            AdapterObservationSubmission submission,
            CancellationToken cancellationToken)
        {
            this.Records.AddRange(submission.Records);
            AdapterObservationResult[] results = submission.Records.Select(record =>
                new AdapterObservationResult(
                    record.OperationId,
                    disposition,
                    disposition == AdapterObservationDisposition.Rejected ? null : Guid.NewGuid(),
                    disposition == AdapterObservationDisposition.Rejected ? "test.rejected" : null)).ToArray();
            bool checkpointAccepted = disposition != AdapterObservationDisposition.Rejected;
            return Task.FromResult(new AdapterObservationAcknowledgement(
                submission.RunId,
                submission.LeaseId,
                results,
                checkpointAccepted,
                checkpointAccepted ? submission.ProposedCheckpoint : null));
        }
    }

    private sealed class MismatchedSink : IAdapterObservationSink
    {
        public Task<AdapterObservationAcknowledgement> SubmitAsync(
            AdapterObservationSubmission submission,
            CancellationToken cancellationToken) =>
            Task.FromResult(new AdapterObservationAcknowledgement(
                Guid.NewGuid(),
                submission.LeaseId,
                submission.Records.Select(record => new AdapterObservationResult(
                    record.OperationId,
                    AdapterObservationDisposition.Accepted,
                    Guid.NewGuid(),
                    errorCode: null)).ToArray(),
                checkpointAccepted: true,
                submission.ProposedCheckpoint));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
