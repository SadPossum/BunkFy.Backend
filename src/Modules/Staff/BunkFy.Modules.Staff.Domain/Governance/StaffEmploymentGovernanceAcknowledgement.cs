namespace BunkFy.Modules.Staff.Domain.Governance;

using BunkFy.Modules.Staff.Domain.Errors;
using Gma.Framework.Results;

public sealed class StaffEmploymentGovernanceAcknowledgement
    : IEquatable<StaffEmploymentGovernanceAcknowledgement>
{
    public const int MaximumAcknowledgements = 64;
    public const int IdMaxLength = 128;

    private StaffEmploymentGovernanceAcknowledgement() { }

    private StaffEmploymentGovernanceAcknowledgement(
        string acknowledgementId,
        int acknowledgementVersion)
    {
        this.AcknowledgementId = acknowledgementId;
        this.AcknowledgementVersion = acknowledgementVersion;
    }

    public string AcknowledgementId { get; private set; } = string.Empty;
    public int AcknowledgementVersion { get; private set; }

    public static Result<StaffEmploymentGovernanceAcknowledgement> Create(
        string? acknowledgementId,
        int acknowledgementVersion)
    {
        string normalized = acknowledgementId?.Trim() ?? string.Empty;
        if (!StaffEmploymentGovernanceBinding.IsKey(normalized) ||
            acknowledgementVersion <= 0)
        {
            return Result.Failure<StaffEmploymentGovernanceAcknowledgement>(
                StaffDomainErrors.EmploymentGovernanceAcknowledgementsInvalid);
        }

        return Result.Success(
            new StaffEmploymentGovernanceAcknowledgement(
                normalized,
                acknowledgementVersion));
    }

    public bool Equals(StaffEmploymentGovernanceAcknowledgement? other) =>
        other is not null &&
        string.Equals(
            this.AcknowledgementId,
            other.AcknowledgementId,
            StringComparison.Ordinal) &&
        this.AcknowledgementVersion == other.AcknowledgementVersion;

    public override bool Equals(object? obj) =>
        this.Equals(obj as StaffEmploymentGovernanceAcknowledgement);

    public override int GetHashCode() =>
        HashCode.Combine(
            this.AcknowledgementId,
            this.AcknowledgementVersion);
}
