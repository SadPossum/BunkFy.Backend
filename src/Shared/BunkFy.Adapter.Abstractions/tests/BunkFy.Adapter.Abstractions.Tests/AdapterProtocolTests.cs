namespace BunkFy.Adapter.Abstractions.Tests;

using System.Text;
using Xunit;

[Trait("Category", "Unit")]
public sealed class AdapterProtocolTests
{
    [Fact]
    public void Configuration_material_does_not_render_or_retain_secret_after_disposal()
    {
        byte[] secret = "do-not-log-this"u8.ToArray();
        AdapterConfigurationMaterial material = new(
            schemaVersion: 1,
            "application/json",
            "{}"u8,
            "text/plain",
            secret);

        Assert.DoesNotContain("do-not-log-this", material.ToString(), StringComparison.Ordinal);
        Assert.True(material.HasSecret);

        material.Dispose();

        Assert.False(material.HasSecret);
        Assert.Throws<ObjectDisposedException>(() => _ = material.Secret);
    }

    private static readonly DateTimeOffset Now = new(2026, 7, 12, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Descriptor_normalizes_type_and_requires_known_execution_modes()
    {
        AdapterDescriptor descriptor = new(" Booking.Email ", 1, 2, [AdapterExecutionMode.Polling]);

        Assert.Equal("booking.email", descriptor.AdapterType);
        Assert.Equal(1, descriptor.ProtocolVersion);
        Assert.Equal(2, descriptor.ConfigurationSchemaVersion);
        Assert.Equal([AdapterExecutionMode.Polling], descriptor.ExecutionModes);
        Assert.Throws<ArgumentException>(() => new AdapterDescriptor("booking/email", 1, 1, [AdapterExecutionMode.Polling]));
        Assert.Throws<ArgumentException>(() => new AdapterDescriptor("réservation.email", 1, 1, [AdapterExecutionMode.Polling]));
        Assert.Throws<ArgumentException>(() => new AdapterDescriptor("booking.email", 1, 1, [AdapterExecutionMode.Unknown]));
    }

    [Fact]
    public void Polling_capability_is_bounded_and_requires_polling_mode()
    {
        AdapterPollingCapability polling = new(TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(2));
        AdapterDescriptor descriptor = new(
            "booking.email", 1, 1, [AdapterExecutionMode.Polling], polling);

        Assert.Equal(TimeSpan.FromSeconds(10), descriptor.Polling?.MinimumInterval);
        Assert.Equal(TimeSpan.FromMinutes(2), descriptor.Polling?.RecommendedInterval);
        Assert.Throws<ArgumentException>(() => new AdapterDescriptor(
            "booking.email", 1, 1, [AdapterExecutionMode.Push], polling));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AdapterPollingCapability(
            TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void Run_assignment_requires_a_bounded_lease_and_preserves_opaque_checkpoint()
    {
        AdapterRunAssignment assignment = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            " tenant-a ",
            Guid.NewGuid(),
            "ota.booking",
            AdapterExecutionMode.Polling,
            Now,
            Now.AddMinutes(5),
            " cursor-42 ");

        Assert.Equal("tenant-a", assignment.ScopeId);
        Assert.Equal("cursor-42", assignment.Checkpoint);
        Assert.Throws<ArgumentException>(() => new AdapterRunAssignment(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "tenant-a", Guid.NewGuid(), "ota.booking",
            AdapterExecutionMode.Polling, Now, Now, null));
    }

    [Fact]
    public void Observation_copies_payload_and_verifies_its_hash()
    {
        byte[] payload = Encoding.UTF8.GetBytes("{\"reservation\":42}");
        string hash = AdapterPayloadHash.ComputeSha256(payload);
        AdapterObservedRecord record = new(
            Guid.NewGuid(),
            " Reservation.Change ",
            " booking-42 ",
            " 7 ",
            Now.AddMinutes(-1),
            Now,
            " Application/Json ",
            payload,
            hash.ToUpperInvariant());

        payload[0] = 0;

        Assert.Equal("reservation.change", record.RecordType);
        Assert.Equal("booking-42", record.ExternalRecordId);
        Assert.Equal("application/json", record.ContentType);
        Assert.Equal((byte)'{', record.Payload.Span[0]);
        Assert.Equal(hash, record.ContentSha256);
        Assert.Throws<ArgumentException>(() => new AdapterObservedRecord(
            Guid.NewGuid(), "reservation.change", "booking-42", "8", Now, Now, "application/json",
            Encoding.UTF8.GetBytes("{}"), hash));
    }

    [Fact]
    public void Submission_rejects_duplicate_operations_and_oversized_batches()
    {
        Guid operationId = Guid.NewGuid();
        AdapterObservedRecord record = CreateRecord(operationId, "1");

        Assert.Throws<ArgumentException>(() => new AdapterObservationSubmission(
            Guid.NewGuid(), Guid.NewGuid(), [record, record], "cursor-1"));
        Assert.Throws<ArgumentException>(() => new AdapterObservationSubmission(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Enumerable.Range(0, AdapterProtocolLimits.MaximumRecordsPerSubmission + 1)
                .Select(index => CreateRecord(Guid.NewGuid(), index.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                .ToArray(),
            "cursor-1"));

        byte[] maximumPayload = new byte[AdapterProtocolLimits.MaximumInlinePayloadBytes];
        AdapterObservedRecord[] aggregateTooLarge = Enumerable.Range(0, 5)
            .Select(index => new AdapterObservedRecord(
                Guid.NewGuid(), "reservation.change", $"large-{index}",
                index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Now, Now, "application/octet-stream", maximumPayload,
                AdapterPayloadHash.ComputeSha256(maximumPayload)))
            .ToArray();
        Assert.Throws<ArgumentException>(() => new AdapterObservationSubmission(
            Guid.NewGuid(), Guid.NewGuid(), aggregateTooLarge, "cursor-1"));
    }

    [Fact]
    public void Acknowledgement_requires_a_checkpoint_only_after_durable_acceptance()
    {
        AdapterObservationResult result = new(
            Guid.NewGuid(),
            AdapterObservationDisposition.Accepted,
            Guid.NewGuid(),
            null);
        AdapterObservationAcknowledgement acknowledgement = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            [result],
            checkpointAccepted: true,
            "cursor-2");

        Assert.True(acknowledgement.CheckpointAccepted);
        Assert.Equal("cursor-2", acknowledgement.AcceptedCheckpoint);
        Assert.Throws<ArgumentException>(() => new AdapterObservationAcknowledgement(
            Guid.NewGuid(), Guid.NewGuid(), [result], checkpointAccepted: true, acceptedCheckpoint: null));
        AdapterObservationResult rejected = new(
            Guid.NewGuid(), AdapterObservationDisposition.Rejected, receiptId: null, errorCode: "invalid-envelope");
        Assert.Equal("invalid-envelope", rejected.ErrorCode);
        Assert.Throws<ArgumentException>(() => new AdapterObservationAcknowledgement(
            Guid.NewGuid(), Guid.NewGuid(), [rejected], checkpointAccepted: true, acceptedCheckpoint: "cursor-3"));
        Assert.Throws<ArgumentException>(() => new AdapterObservationResult(
            Guid.NewGuid(), AdapterObservationDisposition.Accepted, receiptId: null, errorCode: null));
        Assert.Throws<ArgumentException>(() => new AdapterObservationResult(
            Guid.NewGuid(), AdapterObservationDisposition.Rejected, receiptId: null, errorCode: "réservation.invalid"));
    }

    [Fact]
    public void Run_completion_validates_observation_counts()
    {
        AdapterRunCompletion completion = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            AdapterRunOutcome.PartiallySucceeded,
            observedCount: 3,
            acceptedCount: 2,
            rejectedCount: 1,
            "cursor-2",
            errorCode: "provider.partial",
            errorMessage: null);

        Assert.Equal(3, completion.ObservedCount);
        Assert.Equal("provider.partial", completion.ErrorCode);
        Assert.Throws<ArgumentException>(() => new AdapterRunCompletion(
            Guid.NewGuid(),
            Guid.NewGuid(),
            AdapterRunOutcome.Succeeded,
            observedCount: 1,
            acceptedCount: 1,
            rejectedCount: 1,
            acceptedCheckpoint: null,
            errorCode: null,
            errorMessage: null));
        Assert.Throws<ArgumentException>(() => new AdapterRunCompletion(
            Guid.NewGuid(),
            Guid.NewGuid(),
            AdapterRunOutcome.Succeeded,
            observedCount: int.MaxValue,
            acceptedCount: int.MaxValue,
            rejectedCount: int.MaxValue,
            acceptedCheckpoint: null,
            errorCode: null,
            errorMessage: null));
    }

    private static AdapterObservedRecord CreateRecord(Guid operationId, string revision)
    {
        byte[] payload = Encoding.UTF8.GetBytes($"{{\"revision\":\"{revision}\"}}");
        return new(
            operationId,
            "reservation.change",
            $"booking-{revision}",
            revision,
            Now,
            Now,
            "application/json",
            payload,
            AdapterPayloadHash.ComputeSha256(payload));
    }
}
