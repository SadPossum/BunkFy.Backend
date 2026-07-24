namespace BunkFy.Modules.DataRights.Domain.Aggregates;

using BunkFy.Modules.DataRights.Domain.Entities;
using BunkFy.Modules.DataRights.Domain.Errors;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class DataRightsExecutionWorkItem : ScopedAggregateRoot<Guid>
{
    private DataRightsExecutionWorkItem() { }

    private DataRightsExecutionWorkItem(Guid id, string scopeId) : base(id, scopeId) { }

    public Guid IdempotencyKey { get; private set; }
    public Guid CaseId { get; private set; }
    public Guid PropertyId { get; private set; }
    public long ApprovalRevision { get; private set; }
    public long ExecutionRevision { get; private set; }
    public DataRightsCaseOperation Operation { get; private set; }
    public string OwnerKey { get; private set; } = string.Empty;
    public string RecordType { get; private set; } = string.Empty;
    public Guid RecordId { get; private set; }
    public long SelectedRecordVersion { get; private set; }
    public int PolicyEvidenceSchemaVersion { get; private set; }
    public string PolicyId { get; private set; } = string.Empty;
    public int PolicyVersion { get; private set; }
    public string RetentionPolicyId { get; private set; } = string.Empty;
    public int RetentionPolicyVersion { get; private set; }
    public string PolicyContentSha256 { get; private set; } = string.Empty;
    public DataRightsExecutionWorkItemState State { get; private set; }
    public int AttemptCount { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public long Version { get; private set; } = 1;

    public static Result<DataRightsExecutionWorkItem> Prepare(
        Guid id,
        string tenantId,
        Guid idempotencyKey,
        Guid caseId,
        Guid propertyId,
        long approvalRevision,
        long executionRevision,
        DataRightsCaseOperation operation,
        DataRightsSubjectCoordinate subject,
        DataRightsApprovalPolicyEvidence policyEvidence,
        string actorId,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(policyEvidence);

        if (id == Guid.Empty ||
            idempotencyKey == Guid.Empty ||
            caseId == Guid.Empty ||
            propertyId == Guid.Empty ||
            approvalRevision <= 0 ||
            executionRevision <= approvalRevision ||
            operation != DataRightsCaseOperation.Anonymisation)
        {
            return Result.Failure<DataRightsExecutionWorkItem>(
                DataRightsDomainErrors.ExecutionCoordinateInvalid);
        }

        if (!TenantIds.TryNormalize(tenantId, out string? scopeId))
        {
            return Result.Failure<DataRightsExecutionWorkItem>(
                DataRightsDomainErrors.TenantInvalid);
        }

        string normalizedActor = actorId?.Trim() ?? string.Empty;
        if (normalizedActor.Length is 0 or > DataRightsCase.ActorIdMaxLength)
        {
            return Result.Failure<DataRightsExecutionWorkItem>(
                DataRightsDomainErrors.ActorInvalid);
        }

        if (nowUtc == default ||
            policyEvidence.PropertyId != propertyId ||
            policyEvidence.SchemaVersion != DataRightsApprovalPolicyEvidence.CurrentSchemaVersion)
        {
            return Result.Failure<DataRightsExecutionWorkItem>(
                DataRightsDomainErrors.ExecutionCoordinateInvalid);
        }

        return Result.Success(new DataRightsExecutionWorkItem(id, scopeId)
        {
            IdempotencyKey = idempotencyKey,
            CaseId = caseId,
            PropertyId = propertyId,
            ApprovalRevision = approvalRevision,
            ExecutionRevision = executionRevision,
            Operation = operation,
            OwnerKey = subject.OwnerKey,
            RecordType = subject.RecordType,
            RecordId = subject.RecordId,
            SelectedRecordVersion = subject.RecordVersion,
            PolicyEvidenceSchemaVersion = policyEvidence.SchemaVersion,
            PolicyId = policyEvidence.PolicyId,
            PolicyVersion = policyEvidence.PolicyVersion,
            RetentionPolicyId = policyEvidence.RetentionPolicyId,
            RetentionPolicyVersion = policyEvidence.RetentionPolicyVersion,
            PolicyContentSha256 = policyEvidence.ContentSha256,
            State = DataRightsExecutionWorkItemState.Prepared,
            CreatedBy = normalizedActor,
            CreatedAtUtc = nowUtc
        });
    }

    public bool HasIdempotencyKey(Guid idempotencyKey) =>
        idempotencyKey != Guid.Empty && this.IdempotencyKey == idempotencyKey;
}
