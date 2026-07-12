namespace BunkFy.Adapters.ImapReservationMail;

using BunkFy.Adapter.Abstractions;

public sealed class ImapReservationMailAdapterDescriptor : IAdapterDescriptorProvider
{
    public const string AdapterType = "imap.reservation-json";

    public static AdapterDescriptor Value { get; } = new(
        AdapterType,
        protocolVersion: 1,
        configurationSchemaVersion: 3,
        [AdapterExecutionMode.Polling, AdapterExecutionMode.Push, AdapterExecutionMode.RemotePolling],
        new AdapterPollingCapability(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5)));

    public AdapterDescriptor Descriptor => Value;
}
