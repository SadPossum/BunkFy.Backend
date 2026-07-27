namespace BunkFy.Modules.DataRights.Contracts;

public interface IDataRightsCorrectionPolicyContributor
{
    int ContractVersion { get; }

    string OwnerKey { get; }

    string RecordType { get; }

    string FieldPolicyKey { get; }
}

public interface IDataRightsCorrectionExecutionGate
{
    Task<DataRightsCorrectionExecutionGateResult> EvaluateAsync(
        DataRightsCorrectionExecutionGateRequest request,
        CancellationToken cancellationToken);
}

public sealed record DataRightsCorrectionExecutionGateRequest(
    string TenantId,
    Guid PropertyId,
    Guid CaseId,
    long ApprovalRevision,
    Guid ExecutionId,
    DataRightsSubjectCoordinate Coordinate,
    string FieldPolicyKey,
    string ExecutingActorId);

public sealed record DataRightsCorrectionExecutionGateResult(
    bool IsAllowed,
    DataRightsCorrectionExecutionDenial Denial,
    DateTimeOffset? ExpiresAtUtc = null)
{
    public static DataRightsCorrectionExecutionGateResult Allowed(
        DateTimeOffset expiresAtUtc) =>
        new(true, DataRightsCorrectionExecutionDenial.None, expiresAtUtc);

    public static DataRightsCorrectionExecutionGateResult Denied(
        DataRightsCorrectionExecutionDenial denial) =>
        new(false, denial);
}

public enum DataRightsCorrectionExecutionDenial
{
    None = 0,
    InvalidRequest = 1,
    ExecutionNotFound = 2,
    CaseNotExecuting = 3,
    ApprovalRevisionMismatch = 4,
    SubjectMismatch = 5,
    ExecutionMismatch = 6,
    ActorMismatch = 7,
    FieldPolicyMismatch = 8,
    ExecutionExpired = 9,
    ExecutionCompleted = 10
}

public static class DataRightsCorrectionContract
{
    public const int CurrentVersion = 1;
    public const int OwnerKeyMaxLength = 100;
    public const int RecordTypeMaxLength = 100;
    public const int FieldPolicyKeyMaxLength = 120;
    public const int FieldKeyMaxLength = 120;
    public const int MaxChangedFieldCount = 32;
    public const int Sha256Length = 64;
}
