namespace BunkFy.Modules.DataRights.Domain.Aggregates;

using BunkFy.Modules.DataRights.Domain.Errors;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class DataRightsExecutionBatch : ScopedAggregateRoot<Guid>
{
    private DataRightsExecutionBatch() { }

    private DataRightsExecutionBatch(Guid id, string scopeId) : base(id, scopeId) { }

    public Guid IdempotencyKey { get; private set; }
    public Guid CaseId { get; private set; }
    public Guid PropertyId { get; private set; }
    public long ApprovalRevision { get; private set; }
    public long ExecutionRevision { get; private set; }
    public int SelectedSubjectCount { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public long Version { get; private set; } = 1;

    public static Result<DataRightsExecutionBatch> Prepare(
        Guid id,
        string tenantId,
        Guid idempotencyKey,
        Guid caseId,
        Guid propertyId,
        long approvalRevision,
        long executionRevision,
        int selectedSubjectCount,
        string actorId,
        DateTimeOffset nowUtc)
    {
        if (id == Guid.Empty ||
            idempotencyKey == Guid.Empty ||
            caseId == Guid.Empty ||
            propertyId == Guid.Empty ||
            approvalRevision <= 0 ||
            executionRevision <= approvalRevision ||
            selectedSubjectCount is <= 0 or > DataRightsCase.MaxSelectedSubjects)
        {
            return Result.Failure<DataRightsExecutionBatch>(
                DataRightsDomainErrors.ExecutionCoordinateInvalid);
        }

        if (!TenantIds.TryNormalize(tenantId, out string? scopeId))
        {
            return Result.Failure<DataRightsExecutionBatch>(
                DataRightsDomainErrors.TenantInvalid);
        }

        string actor = actorId?.Trim() ?? string.Empty;
        if (actor.Length is 0 or > DataRightsCase.ActorIdMaxLength)
        {
            return Result.Failure<DataRightsExecutionBatch>(
                DataRightsDomainErrors.ActorInvalid);
        }

        if (nowUtc == default)
        {
            return Result.Failure<DataRightsExecutionBatch>(
                DataRightsDomainErrors.TimestampInvalid);
        }

        return Result.Success(new DataRightsExecutionBatch(id, scopeId)
        {
            IdempotencyKey = idempotencyKey,
            CaseId = caseId,
            PropertyId = propertyId,
            ApprovalRevision = approvalRevision,
            ExecutionRevision = executionRevision,
            SelectedSubjectCount = selectedSubjectCount,
            CreatedBy = actor,
            CreatedAtUtc = nowUtc
        });
    }

    public bool Matches(
        Guid idempotencyKey,
        Guid caseId,
        Guid propertyId,
        long? executionRevision) =>
        idempotencyKey != Guid.Empty &&
        caseId != Guid.Empty &&
        propertyId != Guid.Empty &&
        executionRevision.HasValue &&
        this.IdempotencyKey == idempotencyKey &&
        this.CaseId == caseId &&
        this.PropertyId == propertyId &&
        this.ExecutionRevision == executionRevision.Value;
}
