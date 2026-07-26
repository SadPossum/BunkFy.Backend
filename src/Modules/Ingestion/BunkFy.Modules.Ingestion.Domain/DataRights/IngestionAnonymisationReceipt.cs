namespace BunkFy.Modules.Ingestion.Domain.DataRights;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BunkFy.Modules.Ingestion.Domain.Errors;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class IngestionAnonymisationReceipt
    : ScopedAggregateRoot<Guid>
{
    public const int CurrentContractVersion = 1;
    public const int ActorIdMaxLength = 200;
    public const int Sha256Length = 64;

    private IngestionAnonymisationReceipt() { }

    private IngestionAnonymisationReceipt(Guid id, string scopeId)
        : base(id, scopeId)
    {
    }

    public int ContractVersion { get; private set; }
    public Guid WorkItemId { get; private set; }
    public Guid IdempotencyKey { get; private set; }
    public Guid PropertyId { get; private set; }
    public Guid CaseId { get; private set; }
    public long ApprovalRevision { get; private set; }
    public long OperationRevision { get; private set; }
    public Guid SourceLinkId { get; private set; }
    public long SelectedSourceLinkVersion { get; private set; }
    public long ResultingSourceLinkVersion { get; private set; }
    public IngestionAnonymisationDisposition Disposition { get; private set; }
    public IngestionAnonymisationReason Reason { get; private set; }
    public int GraphRecordCount { get; private set; }
    public int FingerprintCount { get; private set; }
    public int RawPayloadCount { get; private set; }
    public string ApprovalEvidenceSha256 { get; private set; } =
        string.Empty;
    public string PolicyEvidenceSha256 { get; private set; } =
        string.Empty;
    public string OperationFenceSha256 { get; private set; } =
        string.Empty;
    public string ActorId { get; private set; } = string.Empty;
    public DateTimeOffset CompletedAtUtc { get; private set; }
    public string CanonicalSha256 { get; private set; } = string.Empty;

    public static Result<IngestionAnonymisationReceipt> Create(
        Guid receiptId,
        string tenantId,
        Guid workItemId,
        Guid idempotencyKey,
        Guid propertyId,
        Guid caseId,
        long approvalRevision,
        long operationRevision,
        Guid sourceLinkId,
        long selectedSourceLinkVersion,
        long resultingSourceLinkVersion,
        int graphRecordCount,
        int fingerprintCount,
        int rawPayloadCount,
        string approvalEvidenceSha256,
        string policyEvidenceSha256,
        string operationFenceSha256,
        string actorId,
        DateTimeOffset completedAtUtc)
    {
        string actor = actorId?.Trim() ?? string.Empty;
        string approvalDigest = NormalizeSha256(
            approvalEvidenceSha256);
        string policyDigest = NormalizeSha256(policyEvidenceSha256);
        string operationDigest = NormalizeSha256(
            operationFenceSha256);
        if (receiptId == Guid.Empty ||
            !TenantIds.TryNormalize(tenantId, out string? scopeId) ||
            workItemId == Guid.Empty ||
            idempotencyKey == Guid.Empty ||
            propertyId == Guid.Empty ||
            caseId == Guid.Empty ||
            approvalRevision <= 0 ||
            operationRevision <= approvalRevision ||
            sourceLinkId == Guid.Empty ||
            selectedSourceLinkVersion <= 0 ||
            resultingSourceLinkVersion !=
                selectedSourceLinkVersion + 1 ||
            graphRecordCount <= 0 ||
            fingerprintCount <= 0 ||
            rawPayloadCount < 0 ||
            !IsSha256(approvalDigest) ||
            !IsSha256(policyDigest) ||
            !IsSha256(operationDigest) ||
            actor.Length is 0 or > ActorIdMaxLength ||
            completedAtUtc == default)
        {
            return Invalid();
        }

        IngestionAnonymisationReceipt receipt = new(
            receiptId,
            scopeId)
        {
            ContractVersion = CurrentContractVersion,
            WorkItemId = workItemId,
            IdempotencyKey = idempotencyKey,
            PropertyId = propertyId,
            CaseId = caseId,
            ApprovalRevision = approvalRevision,
            OperationRevision = operationRevision,
            SourceLinkId = sourceLinkId,
            SelectedSourceLinkVersion = selectedSourceLinkVersion,
            ResultingSourceLinkVersion = resultingSourceLinkVersion,
            Disposition = IngestionAnonymisationDisposition.Completed,
            Reason =
                IngestionAnonymisationReason.ProviderEvidenceAnonymised,
            GraphRecordCount = graphRecordCount,
            FingerprintCount = fingerprintCount,
            RawPayloadCount = rawPayloadCount,
            ApprovalEvidenceSha256 = approvalDigest,
            PolicyEvidenceSha256 = policyDigest,
            OperationFenceSha256 = operationDigest,
            ActorId = actor,
            CompletedAtUtc = completedAtUtc.ToUniversalTime()
        };
        receipt.CanonicalSha256 = receipt.ComputeCanonicalSha256();
        return Result.Success(receipt);
    }

    public bool MatchesExecution(
        Guid workItemId,
        Guid idempotencyKey,
        Guid propertyId,
        Guid caseId,
        long approvalRevision,
        long operationRevision,
        Guid sourceLinkId,
        long selectedSourceLinkVersion,
        string approvalEvidenceSha256,
        string actorId) =>
        this.ContractVersion == CurrentContractVersion &&
        this.WorkItemId == workItemId &&
        this.IdempotencyKey == idempotencyKey &&
        this.PropertyId == propertyId &&
        this.CaseId == caseId &&
        this.ApprovalRevision == approvalRevision &&
        this.OperationRevision == operationRevision &&
        this.SourceLinkId == sourceLinkId &&
        this.SelectedSourceLinkVersion == selectedSourceLinkVersion &&
        string.Equals(
            this.ApprovalEvidenceSha256,
            NormalizeSha256(approvalEvidenceSha256),
            StringComparison.Ordinal) &&
        string.Equals(
            this.ActorId,
            actorId?.Trim(),
            StringComparison.Ordinal) &&
        string.Equals(
            this.CanonicalSha256,
            this.ComputeCanonicalSha256(),
            StringComparison.Ordinal);

    public bool MatchesOwnerProof(
        int contractVersion,
        Guid receiptId,
        Guid propertyId,
        Guid sourceLinkId,
        long resultingSourceLinkVersion,
        string canonicalSha256,
        DateTimeOffset completedAtUtc) =>
        this.ContractVersion == contractVersion &&
        this.Id == receiptId &&
        this.PropertyId == propertyId &&
        this.SourceLinkId == sourceLinkId &&
        this.ResultingSourceLinkVersion ==
            resultingSourceLinkVersion &&
        this.CompletedAtUtc == completedAtUtc.ToUniversalTime() &&
        string.Equals(
            this.CanonicalSha256,
            NormalizeSha256(canonicalSha256),
            StringComparison.Ordinal) &&
        string.Equals(
            this.CanonicalSha256,
            this.ComputeCanonicalSha256(),
            StringComparison.Ordinal);

    private string ComputeCanonicalSha256()
    {
        StringBuilder canonical = new();
        Append(canonical, this.ContractVersion);
        Append(canonical, this.Id);
        Append(canonical, this.ScopeId);
        Append(canonical, this.WorkItemId);
        Append(canonical, this.IdempotencyKey);
        Append(canonical, this.PropertyId);
        Append(canonical, this.CaseId);
        Append(canonical, this.ApprovalRevision);
        Append(canonical, this.OperationRevision);
        Append(canonical, this.SourceLinkId);
        Append(canonical, this.SelectedSourceLinkVersion);
        Append(canonical, this.ResultingSourceLinkVersion);
        Append(canonical, (int)this.Disposition);
        Append(canonical, (int)this.Reason);
        Append(canonical, this.GraphRecordCount);
        Append(canonical, this.FingerprintCount);
        Append(canonical, this.RawPayloadCount);
        Append(canonical, this.ApprovalEvidenceSha256);
        Append(canonical, this.PolicyEvidenceSha256);
        Append(canonical, this.OperationFenceSha256);
        Append(canonical, this.ActorId);
        Append(
            canonical,
            this.CompletedAtUtc.ToUniversalTime().ToString(
                "O",
                CultureInfo.InvariantCulture));
        return Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(canonical.ToString())))
            .ToLowerInvariant();
    }

    private static void Append(
        StringBuilder target,
        object value)
    {
        string text = value switch
        {
            Guid id => id.ToString("N"),
            IFormattable formattable =>
                formattable.ToString(
                    format: null,
                    CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
        target.Append(
            text.Length.ToString(CultureInfo.InvariantCulture));
        target.Append(':');
        target.Append(text);
    }

    private static string NormalizeSha256(string? value) =>
        value?.Trim().ToLowerInvariant() ?? string.Empty;

    private static bool IsSha256(string value) =>
        value.Length == Sha256Length &&
        value.All(character =>
            character is (>= '0' and <= '9') or
                (>= 'a' and <= 'f'));

    private static Result<IngestionAnonymisationReceipt> Invalid() =>
        Result.Failure<IngestionAnonymisationReceipt>(
            IngestionDomainErrors.AnonymisationReceiptInvalid);
}

public enum IngestionAnonymisationDisposition
{
    Unknown = 0,
    Completed = 1
}

public enum IngestionAnonymisationReason
{
    Unknown = 0,
    ProviderEvidenceAnonymised = 1
}
