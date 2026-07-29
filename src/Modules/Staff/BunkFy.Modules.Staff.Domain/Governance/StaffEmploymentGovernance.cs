namespace BunkFy.Modules.Staff.Domain.Governance;

using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Domain.Errors;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class StaffEmploymentGovernance : ScopedAggregateRoot<Guid>
{
    public const int ContractVersion = 1;

    private readonly List<StaffEmploymentGovernanceAcknowledgement>
        acceptedAcknowledgements = [];

    private StaffEmploymentGovernance() { }

    private StaffEmploymentGovernance(Guid staffMemberId, string scopeId)
        : base(staffMemberId, scopeId)
    {
    }

    public Guid StaffMemberId => this.Id;
    public int GovernanceContractVersion { get; private set; } =
        ContractVersion;
    public long SelectedStaffVersion { get; private set; }
    public StaffEmploymentGovernanceBinding Binding { get; private set; } =
        null!;
    public IReadOnlyCollection<StaffEmploymentGovernanceAcknowledgement>
        AcceptedAcknowledgements =>
        this.acceptedAcknowledgements.AsReadOnly();
    public string ConfiguredBy { get; private set; } = string.Empty;
    public DateTimeOffset ConfiguredAtUtc { get; private set; }
    public long Version { get; private set; } = 1;

    public static Result<StaffEmploymentGovernance> Configure(
        string tenantId,
        Guid staffMemberId,
        long selectedStaffVersion,
        StaffEmploymentGovernanceBinding binding,
        IReadOnlyCollection<StaffEmploymentGovernanceAcknowledgement>
            acceptedAcknowledgements,
        string actorId,
        DateTimeOffset configuredAtUtc)
    {
        if (staffMemberId == Guid.Empty ||
            selectedStaffVersion < 1 ||
            !TenantIds.TryNormalize(tenantId, out string? scopeId))
        {
            return Result.Failure<StaffEmploymentGovernance>(
                StaffDomainErrors.EmploymentGovernanceIdentityInvalid);
        }

        Result validation = ValidateChange(
            binding,
            acceptedAcknowledgements,
            actorId,
            configuredAtUtc);
        if (validation.IsFailure)
        {
            return Result.Failure<StaffEmploymentGovernance>(
                validation.Error);
        }

        StaffEmploymentGovernance governance =
            new(staffMemberId, scopeId);
        governance.Apply(
            selectedStaffVersion,
            binding,
            acceptedAcknowledgements,
            actorId.Trim(),
            configuredAtUtc);
        return Result.Success(governance);
    }

    public Result Replace(
        long expectedGovernanceVersion,
        long selectedStaffVersion,
        StaffEmploymentGovernanceBinding binding,
        IReadOnlyCollection<StaffEmploymentGovernanceAcknowledgement>
            acceptedAcknowledgements,
        string actorId,
        DateTimeOffset configuredAtUtc)
    {
        if (expectedGovernanceVersion != this.Version)
        {
            return Result.Failure(
                StaffDomainErrors.EmploymentGovernanceVersionConflict);
        }

        if (selectedStaffVersion < 1)
        {
            return Result.Failure(
                StaffDomainErrors.EmploymentGovernanceIdentityInvalid);
        }

        if (selectedStaffVersion < this.SelectedStaffVersion ||
            configuredAtUtc < this.ConfiguredAtUtc)
        {
            return Result.Failure(
                StaffDomainErrors.EmploymentGovernanceLifecycleInvalid);
        }

        Result validation = ValidateChange(
            binding,
            acceptedAcknowledgements,
            actorId,
            configuredAtUtc);
        if (validation.IsFailure)
        {
            return validation;
        }

        this.Apply(
            selectedStaffVersion,
            binding,
            acceptedAcknowledgements,
            actorId.Trim(),
            configuredAtUtc);
        this.Version++;
        return Result.Success();
    }

    private static Result ValidateChange(
        StaffEmploymentGovernanceBinding binding,
        IReadOnlyCollection<StaffEmploymentGovernanceAcknowledgement>
            acceptedAcknowledgements,
        string actorId,
        DateTimeOffset configuredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(acceptedAcknowledgements);

        string normalizedActor = actorId?.Trim() ?? string.Empty;
        if (normalizedActor.Length is 0 or > StaffMember.ActorIdMaxLength ||
            normalizedActor.Any(char.IsControl))
        {
            return Result.Failure(StaffDomainErrors.ActorInvalid);
        }

        if (configuredAtUtc == default ||
            configuredAtUtc < binding.EvaluatedAtUtc ||
            configuredAtUtc >= binding.PolicyExpiresAtUtc)
        {
            return Result.Failure(
                StaffDomainErrors.EmploymentGovernanceLifecycleInvalid);
        }

        if (acceptedAcknowledgements.Count >
                StaffEmploymentGovernanceAcknowledgement
                    .MaximumAcknowledgements ||
            acceptedAcknowledgements.Distinct().Count() !=
                acceptedAcknowledgements.Count)
        {
            return Result.Failure(
                StaffDomainErrors
                    .EmploymentGovernanceAcknowledgementsInvalid);
        }

        return Result.Success();
    }

    private void Apply(
        long selectedStaffVersion,
        StaffEmploymentGovernanceBinding binding,
        IReadOnlyCollection<StaffEmploymentGovernanceAcknowledgement>
            acceptedAcknowledgements,
        string actorId,
        DateTimeOffset configuredAtUtc)
    {
        this.SelectedStaffVersion = selectedStaffVersion;
        this.Binding = binding;
        this.acceptedAcknowledgements.Clear();
        this.acceptedAcknowledgements.AddRange(
            acceptedAcknowledgements
                .OrderBy(
                    acknowledgement =>
                        acknowledgement.AcknowledgementId,
                    StringComparer.Ordinal)
                .ThenBy(
                    acknowledgement =>
                        acknowledgement.AcknowledgementVersion));
        this.ConfiguredBy = actorId;
        this.ConfiguredAtUtc = configuredAtUtc;
    }
}
