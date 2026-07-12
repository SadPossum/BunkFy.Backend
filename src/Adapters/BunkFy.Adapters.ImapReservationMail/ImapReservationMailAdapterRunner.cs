namespace BunkFy.Adapters.ImapReservationMail;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BunkFy.Adapter.Abstractions;
using BunkFy.Parsers.ReservationMail;

internal sealed class ImapReservationMailAdapterRunner : IAdapterRunner
{
    private const string ReservationRecordType = "reservation.v1";
    private const string UnparsedRecordType = "mail.unparsed.v1";
    private const string UntrustedRecordType = "mail.untrusted.v1";
    private const string OversizedRecordType = "mail.oversized.v1";
    private readonly IImapMailboxClientFactory clientFactory;
    private readonly TimeProvider timeProvider;

    public ImapReservationMailAdapterRunner(IImapMailboxClientFactory clientFactory)
        : this(clientFactory, TimeProvider.System)
    {
    }

    internal ImapReservationMailAdapterRunner(
        IImapMailboxClientFactory clientFactory,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(clientFactory);
        ArgumentNullException.ThrowIfNull(timeProvider);
        this.clientFactory = clientFactory;
        this.timeProvider = timeProvider;
    }

    public AdapterDescriptor Descriptor => ImapReservationMailAdapterDescriptor.Value;

    public async Task<AdapterRunCompletion> RunAsync(
        AdapterRunAssignment assignment,
        AdapterConfigurationMaterial material,
        IAdapterObservationSink sink,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        ArgumentNullException.ThrowIfNull(material);
        ArgumentNullException.ThrowIfNull(sink);
        if (assignment.AdapterType != ImapReservationMailAdapterDescriptor.AdapterType ||
            assignment.ExecutionMode != AdapterExecutionMode.Polling)
        {
            throw new InvalidOperationException(
                "The IMAP reservation-mail runner received an incompatible assignment.");
        }

        (ImapReservationMailSettings settings, ImapCredential parsedCredential) =
            ImapReservationMailMaterial.Parse(material);
        using ImapCredential credential = parsedCredential;
        ImapReservationMailCheckpoint? checkpoint = ImapReservationMailCheckpoint.Parse(
            assignment.Checkpoint);
        string mailboxKey = ImapReservationMailCheckpoint.CreateMailboxKey(settings, credential);
        if (checkpoint.HasValue && !string.Equals(
                checkpoint.Value.MailboxKey,
                mailboxKey,
                StringComparison.Ordinal))
        {
            return Failed(assignment, "imap.mailbox-identity-changed");
        }

        await using IImapMailboxSession mailbox = await this.clientFactory.OpenAsync(
            settings,
            credential,
            cancellationToken).ConfigureAwait(false);
        if (mailbox.UidValidity == 0)
        {
            return Failed(assignment, "imap.uid-validity-missing");
        }

        if (checkpoint.HasValue && checkpoint.Value.UidValidity != mailbox.UidValidity)
        {
            return Failed(assignment, "imap.uid-validity-changed");
        }

        uint cursor = checkpoint?.LastUid ?? 0;
        List<PendingObservation> observations = new(settings.MaximumMessagesPerRun);
        int evidenceCount = 0;
        long aggregateBytes = 0;
        while (observations.Count < settings.MaximumMessagesPerRun)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ImapMailboxMessageSummary? summary = await mailbox.GetNextAsync(
                cursor,
                cancellationToken).ConfigureAwait(false);
            if (summary is null)
            {
                break;
            }

            if (summary.Uid <= cursor)
            {
                return Failed(assignment, "imap.uid-order-invalid");
            }

            AdapterObservedRecord record;
            bool evidence;
            if (summary.Size is <= 0 or > int.MaxValue || summary.Size > settings.MaximumMessageBytes)
            {
                record = this.CreateOversizedEvidence(assignment, mailbox.UidValidity, summary);
                evidence = true;
            }
            else
            {
                byte[] message = await mailbox.ReadMessageAsync(
                    summary.Uid,
                    settings.MaximumMessageBytes,
                    cancellationToken).ConfigureAwait(false);
                try
                {
                    (record, evidence) = await this.CreateObservationAsync(
                        assignment,
                        mailbox.UidValidity,
                        summary,
                        settings,
                        credential,
                        message,
                        cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    if (message.Length > 0)
                    {
                        CryptographicOperations.ZeroMemory(message);
                    }
                }
            }

            if (aggregateBytes + record.Payload.Length > AdapterProtocolLimits.MaximumSubmissionPayloadBytes)
            {
                break;
            }

            observations.Add(new(summary.Uid, record));
            aggregateBytes += record.Payload.Length;
            evidenceCount += evidence ? 1 : 0;
            cursor = summary.Uid;
        }

        if (observations.Count == 0)
        {
            return new AdapterRunCompletion(
                assignment.RunId,
                assignment.LeaseId,
                AdapterRunOutcome.Succeeded,
                observedCount: 0,
                acceptedCount: 0,
                rejectedCount: 0,
                assignment.Checkpoint,
                errorCode: null,
                errorMessage: null);
        }

        string proposedCheckpoint = new ImapReservationMailCheckpoint(
            mailboxKey,
            mailbox.UidValidity,
            observations[^1].Uid).Serialize();
        AdapterObservationAcknowledgement acknowledgement = await sink.SubmitAsync(
            new AdapterObservationSubmission(
                assignment.RunId,
                assignment.LeaseId,
                observations.Select(item => item.Record).ToArray(),
                proposedCheckpoint),
            cancellationToken).ConfigureAwait(false);
        if (!Matches(assignment, observations, proposedCheckpoint, acknowledgement))
        {
            return Failed(
                assignment,
                "imap.acknowledgement-mismatch",
                observations.Count);
        }

        int accepted = acknowledgement.Results.Count(result =>
            result.Disposition is AdapterObservationDisposition.Accepted or
                AdapterObservationDisposition.Duplicate);
        int rejected = acknowledgement.Results.Count - accepted;
        if (rejected > 0 || !acknowledgement.CheckpointAccepted)
        {
            return new AdapterRunCompletion(
                assignment.RunId,
                assignment.LeaseId,
                AdapterRunOutcome.PartiallySucceeded,
                observations.Count,
                accepted,
                rejected,
                acknowledgement.AcceptedCheckpoint ?? assignment.Checkpoint,
                rejected > 0 ? "imap.observation-rejected" : "imap.checkpoint-not-accepted",
                "The mailbox batch did not receive a complete durable acknowledgement.");
        }

        return new AdapterRunCompletion(
            assignment.RunId,
            assignment.LeaseId,
            evidenceCount == 0 ? AdapterRunOutcome.Succeeded : AdapterRunOutcome.PartiallySucceeded,
            observations.Count,
            accepted,
            rejectedCount: 0,
            acknowledgement.AcceptedCheckpoint,
            evidenceCount == 0 ? null : "imap.unsupported-message",
            evidenceCount == 0 ? null : "One or more mailbox messages were retained as unsupported evidence.");
    }

    private async Task<(AdapterObservedRecord Record, bool Evidence)> CreateObservationAsync(
        AdapterRunAssignment assignment,
        uint uidValidity,
        ImapMailboxMessageSummary summary,
        ImapReservationMailSettings settings,
        IReservationMailAttachmentKeyResolver keyResolver,
        byte[] messageBytes,
        CancellationToken cancellationToken)
    {
        using ReservationMailAuthenticationResult authenticated =
            await ReservationMailEnvelopeReader.ReadAuthenticatedAsync(
            messageBytes,
            settings.AttachmentFileName,
            settings.MaximumAttachmentBytes,
            keyResolver,
            cancellationToken).ConfigureAwait(false);
        if (authenticated.Disposition == ReservationMailAuthenticationDisposition.Untrusted)
        {
            return (this.CreateRawEvidence(
                assignment,
                uidValidity,
                summary,
                messageBytes,
                UntrustedRecordType), true);
        }

        if (authenticated.Disposition == ReservationMailAuthenticationDisposition.AuthenticatedUnparsed)
        {
            return (this.CreateUnparsedEvidence(assignment, uidValidity, summary, messageBytes), true);
        }

        ReservationMailEnvelopeContent envelope = authenticated.Envelope ??
            throw new InvalidOperationException("Authenticated reservation mail did not contain an envelope.");
        string contentHash = AdapterPayloadHash.ComputeSha256(envelope.Payload);
        AdapterObservedRecord record = new(
            CreateOperationId(
                assignment.ConnectionId,
                uidValidity,
                summary.Uid,
                ReservationRecordType,
                envelope.ExternalRecordId,
                envelope.SourceRevision,
                contentHash),
            ReservationRecordType,
            envelope.ExternalRecordId,
            envelope.SourceRevision,
            envelope.SourceUpdatedAtUtc,
            this.ObservedAt(summary),
            "application/json",
            envelope.Payload,
            contentHash);
        return (record, false);
    }

    private AdapterObservedRecord CreateUnparsedEvidence(
        AdapterRunAssignment assignment,
        uint uidValidity,
        ImapMailboxMessageSummary summary,
        byte[] messageBytes) => this.CreateRawEvidence(
        assignment,
        uidValidity,
        summary,
        messageBytes,
        UnparsedRecordType);

    private AdapterObservedRecord CreateRawEvidence(
        AdapterRunAssignment assignment,
        uint uidValidity,
        ImapMailboxMessageSummary summary,
        byte[] messageBytes,
        string recordType)
    {
        byte[] payload = messageBytes.Length == 0
            ? CreateEvidencePayload(uidValidity, summary, "imap.empty-message")
            : messageBytes;
        bool clearPayload = messageBytes.Length == 0;
        try
        {
            string contentType = clearPayload ? "application/json" : "message/rfc822";
            string externalId = MailboxExternalId(uidValidity, summary.Uid);
            string sourceRevision = summary.Uid.ToString(CultureInfo.InvariantCulture);
            string hash = AdapterPayloadHash.ComputeSha256(payload);
            return new(
                CreateOperationId(
                    assignment.ConnectionId,
                    uidValidity,
                    summary.Uid,
                    recordType,
                    externalId,
                    sourceRevision,
                    hash),
                recordType,
                externalId,
                sourceRevision,
                summary.InternalDateUtc,
                this.ObservedAt(summary),
                contentType,
                payload,
                hash);
        }
        finally
        {
            if (clearPayload)
            {
                CryptographicOperations.ZeroMemory(payload);
            }
        }
    }

    private AdapterObservedRecord CreateOversizedEvidence(
        AdapterRunAssignment assignment,
        uint uidValidity,
        ImapMailboxMessageSummary summary)
    {
        byte[] payload = CreateEvidencePayload(uidValidity, summary, "imap.message-too-large");
        try
        {
            string externalId = MailboxExternalId(uidValidity, summary.Uid);
            string sourceRevision = summary.Uid.ToString(CultureInfo.InvariantCulture);
            string hash = AdapterPayloadHash.ComputeSha256(payload);
            return new(
                CreateOperationId(
                    assignment.ConnectionId,
                    uidValidity,
                    summary.Uid,
                    OversizedRecordType,
                    externalId,
                    sourceRevision,
                    hash),
                OversizedRecordType,
                externalId,
                sourceRevision,
                summary.InternalDateUtc,
                this.ObservedAt(summary),
                "application/json",
                payload,
                hash);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(payload);
        }
    }

    private static byte[] CreateEvidencePayload(
        uint uidValidity,
        ImapMailboxMessageSummary summary,
        string errorCode) =>
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 1,
            uidValidity,
            uid = summary.Uid,
            messageSize = summary.Size,
            errorCode
        });

    private static bool Matches(
        AdapterRunAssignment assignment,
        IReadOnlyCollection<PendingObservation> observations,
        string proposedCheckpoint,
        AdapterObservationAcknowledgement acknowledgement) =>
        acknowledgement.RunId == assignment.RunId &&
        acknowledgement.LeaseId == assignment.LeaseId &&
        acknowledgement.Results.Count == observations.Count &&
        acknowledgement.Results.Select(result => result.OperationId).ToHashSet().SetEquals(
            observations.Select(item => item.Record.OperationId)) &&
        (!acknowledgement.CheckpointAccepted || string.Equals(
            acknowledgement.AcceptedCheckpoint,
            proposedCheckpoint,
            StringComparison.Ordinal));

    private static Guid CreateOperationId(
        Guid connectionId,
        uint uidValidity,
        uint uid,
        string recordType,
        string externalRecordId,
        string sourceRevision,
        string contentHash)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, connectionId.ToString("N"));
        Append(hash, uidValidity.ToString(CultureInfo.InvariantCulture));
        Append(hash, uid.ToString(CultureInfo.InvariantCulture));
        Append(hash, recordType);
        Append(hash, externalRecordId);
        Append(hash, sourceRevision);
        Append(hash, contentHash);
        return new Guid(hash.GetHashAndReset().AsSpan(0, 16));
    }

    private static void Append(IncrementalHash hash, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> length = stackalloc byte[sizeof(int)];
        BitConverter.TryWriteBytes(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }

    private DateTimeOffset ObservedAt(ImapMailboxMessageSummary summary) =>
        summary.InternalDateUtc ?? this.timeProvider.GetUtcNow();

    private static string MailboxExternalId(uint uidValidity, uint uid) =>
        $"imap:{uidValidity.ToString(CultureInfo.InvariantCulture)}:{uid.ToString(CultureInfo.InvariantCulture)}";

    private static AdapterRunCompletion Failed(
        AdapterRunAssignment assignment,
        string errorCode,
        int observedCount = 0) =>
        new(
            assignment.RunId,
            assignment.LeaseId,
            AdapterRunOutcome.Failed,
            observedCount,
            acceptedCount: 0,
            rejectedCount: 0,
            assignment.Checkpoint,
            errorCode,
            "The IMAP reservation-mail cycle could not safely advance its checkpoint.");

    private sealed record PendingObservation(uint Uid, AdapterObservedRecord Record);

}
