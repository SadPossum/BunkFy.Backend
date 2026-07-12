namespace BunkFy.Adapter.Runtime;

using BunkFy.Adapter.Abstractions;

public sealed record AdapterRuntimeIdentity
{
    public AdapterRuntimeIdentity(
        string scopeId,
        Guid propertyId,
        Guid connectionId,
        string adapterType,
        TimeSpan assignmentLease)
    {
        string normalizedScope = scopeId?.Trim() ?? string.Empty;
        string normalizedAdapterType = adapterType?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalizedScope.Length is 0 or > 128 ||
            normalizedScope.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
        {
            throw new ArgumentException("The adapter runtime scope id is invalid.", nameof(scopeId));
        }

        if (propertyId == Guid.Empty)
        {
            throw new ArgumentException("A property id is required.", nameof(propertyId));
        }

        if (connectionId == Guid.Empty)
        {
            throw new ArgumentException("A connection id is required.", nameof(connectionId));
        }

        if (normalizedAdapterType.Length is 0 or > AdapterProtocolLimits.AdapterTypeMaxLength ||
            !char.IsLetterOrDigit(normalizedAdapterType[0]) ||
            normalizedAdapterType.Any(character =>
                !char.IsLetterOrDigit(character) && character is not '.' and not '-' and not '_'))
        {
            throw new ArgumentException("The adapter type is invalid.", nameof(adapterType));
        }

        if (assignmentLease < TimeSpan.FromSeconds(30) || assignmentLease > TimeSpan.FromHours(24))
        {
            throw new ArgumentException(
                "The local assignment lease must be between 30 seconds and 24 hours.",
                nameof(assignmentLease));
        }

        this.ScopeId = normalizedScope;
        this.PropertyId = propertyId;
        this.ConnectionId = connectionId;
        this.AdapterType = normalizedAdapterType;
        this.AssignmentLease = assignmentLease;
    }

    public string ScopeId { get; }
    public Guid PropertyId { get; }
    public Guid ConnectionId { get; }
    public string AdapterType { get; }
    public TimeSpan AssignmentLease { get; }
}
