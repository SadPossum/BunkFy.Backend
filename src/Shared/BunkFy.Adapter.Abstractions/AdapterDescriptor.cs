namespace BunkFy.Adapter.Abstractions;

public sealed record AdapterDescriptor
{
    public AdapterDescriptor(
        string adapterType,
        int protocolVersion,
        int configurationSchemaVersion,
        IReadOnlyCollection<AdapterExecutionMode> executionModes,
        AdapterPollingCapability? polling = null)
    {
        this.AdapterType = AdapterProtocolGuards.StableKey(
            adapterType,
            AdapterProtocolLimits.AdapterTypeMaxLength,
            nameof(adapterType));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(protocolVersion);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(configurationSchemaVersion);

        ArgumentNullException.ThrowIfNull(executionModes);
        AdapterExecutionMode[] modes = executionModes.Distinct().ToArray();
        if (modes.Length == 0 || modes.Contains(AdapterExecutionMode.Unknown) || modes.Any(mode => !Enum.IsDefined(mode)))
        {
            throw new ArgumentException("At least one known execution mode is required.", nameof(executionModes));
        }

        if (polling is not null && !modes.Contains(AdapterExecutionMode.Polling))
        {
            throw new ArgumentException("Polling capability requires polling execution mode.", nameof(polling));
        }

        this.ProtocolVersion = protocolVersion;
        this.ConfigurationSchemaVersion = configurationSchemaVersion;
        this.ExecutionModes = Array.AsReadOnly(modes);
        this.Polling = polling;
    }

    public string AdapterType { get; }
    public int ProtocolVersion { get; }
    public int ConfigurationSchemaVersion { get; }
    public IReadOnlyCollection<AdapterExecutionMode> ExecutionModes { get; }
    public AdapterPollingCapability? Polling { get; }
}
