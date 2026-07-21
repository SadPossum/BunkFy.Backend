namespace BunkFy.Modules.Workspaces.Domain;

using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class WorkspaceStaffAccessPlan : ScopedAggregateRoot<Guid>
{
    public const int ProfileKeyMaxLength = 128;
    public const int SubjectIdMaxLength = 256;
    public const int PropertyCountMax = 100;

    private readonly List<WorkspaceStaffAccessPlanProperty> properties = [];

    private WorkspaceStaffAccessPlan() { }
    private WorkspaceStaffAccessPlan(Guid id, string scopeId) : base(id, scopeId) { }

    public WorkspaceStaffOnboardingSource SourceKind { get; private set; }
    public Guid ProfileId { get; private set; }
    public string ProfileKey { get; private set; } = string.Empty;
    public string CreatedBySubjectId { get; private set; } = string.Empty;
    public WorkspaceStaffAccessPlanState Status { get; private set; }
    public long Version { get; private set; } = 1;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset LastChangedAtUtc { get; private set; }
    public IReadOnlyCollection<WorkspaceStaffAccessPlanProperty> Properties =>
        this.properties.AsReadOnly();

    public static Result<WorkspaceStaffAccessPlan> Create(
        Guid sourceId,
        string scopeId,
        WorkspaceStaffOnboardingSource sourceKind,
        Guid profileId,
        string profileKey,
        IReadOnlyCollection<Guid> propertyIds,
        string createdBySubjectId,
        DateTimeOffset nowUtc)
    {
        string key = profileKey?.Trim() ?? string.Empty;
        string actor = createdBySubjectId?.Trim() ?? string.Empty;
        Guid[] properties = propertyIds?.Distinct().Order().ToArray() ?? [];
        if (sourceId == Guid.Empty || profileId == Guid.Empty ||
            sourceKind is not (WorkspaceStaffOnboardingSource.Invitation or
                WorkspaceStaffOnboardingSource.EnrollmentLink) ||
            !TenantIds.TryNormalize(scopeId, out string? normalizedScope) ||
            key.Length is 0 or > ProfileKeyMaxLength ||
            actor.Length is 0 or > SubjectIdMaxLength ||
            properties.Length > PropertyCountMax ||
            properties.Any(propertyId => propertyId == Guid.Empty))
        {
            return Result.Failure<WorkspaceStaffAccessPlan>(Invalid());
        }

        WorkspaceStaffAccessPlan plan = new(sourceId, normalizedScope)
        {
            SourceKind = sourceKind,
            ProfileId = profileId,
            ProfileKey = key,
            CreatedBySubjectId = actor,
            Status = WorkspaceStaffAccessPlanState.Prepared,
            CreatedAtUtc = nowUtc,
            LastChangedAtUtc = nowUtc
        };
        plan.properties.AddRange(properties.Select(propertyId =>
            new WorkspaceStaffAccessPlanProperty(normalizedScope, sourceId, propertyId)));
        return Result.Success(plan);
    }

    public bool Matches(
        WorkspaceStaffOnboardingSource sourceKind,
        Guid profileId,
        string profileKey,
        IReadOnlyCollection<Guid> propertyIds,
        string createdBySubjectId)
    {
        Guid[] expected = propertyIds.Distinct().Order().ToArray();
        return this.SourceKind == sourceKind &&
            this.ProfileId == profileId &&
            string.Equals(this.ProfileKey, profileKey.Trim(), StringComparison.Ordinal) &&
            string.Equals(this.CreatedBySubjectId, createdBySubjectId.Trim(), StringComparison.Ordinal) &&
            this.properties.Select(property => property.PropertyId).Order().SequenceEqual(expected);
    }

    public Result Activate(DateTimeOffset nowUtc)
    {
        if (this.Status == WorkspaceStaffAccessPlanState.Active)
        {
            return Result.Success();
        }

        if (this.Status != WorkspaceStaffAccessPlanState.Prepared)
        {
            return Result.Failure(WorkspaceStaffAccessPlanErrors.StateConflict);
        }

        this.Status = WorkspaceStaffAccessPlanState.Active;
        this.Advance(nowUtc);
        return Result.Success();
    }

    public Result Supersede(DateTimeOffset nowUtc)
    {
        if (this.Status == WorkspaceStaffAccessPlanState.Superseded)
        {
            return Result.Success();
        }

        this.Status = WorkspaceStaffAccessPlanState.Superseded;
        this.Advance(nowUtc);
        return Result.Success();
    }

    private static Error Invalid() => WorkspaceStaffAccessPlanErrors.Invalid;

    private void Advance(DateTimeOffset nowUtc)
    {
        this.Version++;
        this.LastChangedAtUtc = nowUtc;
    }
}
