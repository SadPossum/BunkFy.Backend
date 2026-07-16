namespace BunkFy.Modules.Staff.Domain.Aggregates;

using Gma.Framework.Domain.Models;
using Gma.Framework.Results;
using BunkFy.Modules.Staff.Domain.Entities;
using BunkFy.Modules.Staff.Domain.Errors;
using BunkFy.Modules.Staff.Domain.Events;
using BunkFy.Modules.Staff.Domain.ValueObjects;

public sealed partial class StaffMember : ScopedAggregateRoot<Guid>
{
    public const int DisplayNameMaxLength = StaffProfile.DisplayNameMaxLength;
    public const int LegalNameMaxLength = StaffProfile.LegalNameMaxLength;
    public const int EmailMaxLength = StaffProfile.EmailMaxLength;
    public const int PhoneMaxLength = StaffProfile.PhoneMaxLength;
    public const int EmployeeNumberMaxLength = StaffProfile.EmployeeNumberMaxLength;
    public const int JobTitleMaxLength = StaffProfile.JobTitleMaxLength;
    public const int DepartmentMaxLength = StaffProfile.DepartmentMaxLength;
    public const int AuthSubjectIdMaxLength = StaffProfile.AuthSubjectIdMaxLength;
    public const int ActorIdMaxLength = StaffActorId.MaxLength;
    public const int ReasonMaxLength = StaffChangeReason.MaxLength;

    private readonly List<StaffPropertyAssignment> assignments = [];

    private StaffMember() { }
    private StaffMember(Guid id, string scopeId) : base(id, scopeId) { }

    public string DisplayName { get; private set; } = string.Empty;
    public string DisplayNameSearch { get; private set; } = string.Empty;
    public string? LegalName { get; private set; }
    public string? LegalNameSearch { get; private set; }
    public string? WorkEmail { get; private set; }
    public string? WorkEmailSearch { get; private set; }
    public string? WorkPhone { get; private set; }
    public string? WorkPhoneSearch { get; private set; }
    public string? EmployeeNumber { get; private set; }
    public string? EmployeeNumberSearch { get; private set; }
    public string? JobTitle { get; private set; }
    public string? Department { get; private set; }
    public string? AuthSubjectId { get; private set; }
    public StaffMemberState Status { get; private set; } = StaffMemberState.Active;
    public long Version { get; private set; } = 1;
    public long ProjectionOrdinal { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public string LastChangedBy { get; private set; } = string.Empty;
    public DateTimeOffset LastChangedAtUtc { get; private set; }
    public DateTimeOffset? SuspendedAtUtc { get; private set; }
    public DateTimeOffset? DepartedAtUtc { get; private set; }
    public DateOnly? DepartureEffectiveOn { get; private set; }
    public IReadOnlyCollection<StaffPropertyAssignment> Assignments => this.assignments.AsReadOnly();

    private Result EnsureMutable(long expectedVersion, Guid eventId)
    {
        if (expectedVersion != this.Version)
        {
            return Result.Failure(StaffDomainErrors.VersionConflict);
        }

        if (this.Status == StaffMemberState.Departed)
        {
            return Result.Failure(StaffDomainErrors.StaffDeparted);
        }

        return eventId == Guid.Empty ? Result.Failure(StaffDomainErrors.EventIdRequired) : Result.Success();
    }

    private Result EnsureActive(long expectedVersion, Guid eventId)
    {
        Result mutable = this.EnsureMutable(expectedVersion, eventId);
        if (mutable.IsFailure)
        {
            return mutable;
        }

        return this.Status == StaffMemberState.Suspended
            ? Result.Failure(StaffDomainErrors.StaffSuspended)
            : Result.Success();
    }

    private void ApplyProfile(StaffProfile profile)
    {
        this.DisplayName = profile.DisplayName;
        this.DisplayNameSearch = profile.DisplayNameSearch;
        this.LegalName = profile.LegalName;
        this.LegalNameSearch = profile.LegalNameSearch;
        this.WorkEmail = profile.WorkEmail;
        this.WorkEmailSearch = profile.WorkEmailSearch;
        this.WorkPhone = profile.WorkPhone;
        this.WorkPhoneSearch = profile.WorkPhoneSearch;
        this.EmployeeNumber = profile.EmployeeNumber;
        this.EmployeeNumberSearch = profile.EmployeeNumberSearch;
        this.JobTitle = profile.JobTitle;
        this.Department = profile.Department;
        this.AuthSubjectId = profile.AuthSubjectId;
    }

    private void Advance(StaffActorId actor, DateTimeOffset nowUtc)
    {
        this.Version++;
        this.LastChangedBy = actor.Value;
        this.LastChangedAtUtc = nowUtc;
    }

    private void RaiseAssignmentEvent(StaffPropertyAssignment assignment, Guid eventId, DateTimeOffset nowUtc) =>
        this.RaiseDomainEvent(new StaffPropertyAssignmentChangedDomainEvent(eventId, nowUtc, this.ScopeId,
            this.Id, assignment.Id, assignment.PropertyId, assignment.IsCurrent, assignment.IsPrimary,
            assignment.EffectiveFrom, assignment.EffectiveTo, this.Version, this.LastChangedBy));
}
