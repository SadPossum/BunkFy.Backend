namespace BunkFy.Modules.Properties.Domain.ValueObjects;

using BunkFy.Modules.Properties.Domain.Aggregates;
using BunkFy.Modules.Properties.Domain.Errors;
using Gma.Framework.Results;

public sealed class PropertyGovernanceAcknowledgement : IEquatable<PropertyGovernanceAcknowledgement>
{
    public const int MaximumAcknowledgements = 64;

    private PropertyGovernanceAcknowledgement() { }

    private PropertyGovernanceAcknowledgement(string acknowledgementId, int acknowledgementVersion)
    {
        this.AcknowledgementId = acknowledgementId;
        this.AcknowledgementVersion = acknowledgementVersion;
    }

    public string AcknowledgementId { get; private set; } = string.Empty;
    public int AcknowledgementVersion { get; private set; }

    public static Result<PropertyGovernanceAcknowledgement> Create(
        string? acknowledgementId,
        int acknowledgementVersion)
    {
        string normalized = acknowledgementId?.Trim() ?? string.Empty;
        if (normalized.Length is 0 or > Property.PolicyKeyMaxLength ||
            normalized[0] is < 'a' or > 'z' ||
            normalized.Any(character =>
                character is not (>= 'a' and <= 'z') and
                    not (>= '0' and <= '9') and
                    not '.' and not '-' and not '_') ||
            acknowledgementVersion <= 0)
        {
            return Result.Failure<PropertyGovernanceAcknowledgement>(
                PropertiesDomainErrors.PolicyAcknowledgementsInvalid);
        }

        return Result.Success(new PropertyGovernanceAcknowledgement(normalized, acknowledgementVersion));
    }

    public bool Equals(PropertyGovernanceAcknowledgement? other) =>
        other is not null &&
        string.Equals(this.AcknowledgementId, other.AcknowledgementId, StringComparison.Ordinal) &&
        this.AcknowledgementVersion == other.AcknowledgementVersion;

    public override bool Equals(object? obj) => this.Equals(obj as PropertyGovernanceAcknowledgement);

    public override int GetHashCode() => HashCode.Combine(this.AcknowledgementId, this.AcknowledgementVersion);
}
