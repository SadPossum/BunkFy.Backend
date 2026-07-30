namespace BunkFy.Modules.Workspaces.Domain.DataRights;

using BunkFy.Modules.Workspaces.Domain.Events;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class WorkspaceStaffOnboardingCorrectionReceipt
    : ScopedAggregateRoot<Guid>
{
    public const int CurrentContractVersion = 1;
    public const int DigestLength = 64;
    public const int AllChangedFieldsMask = (1 << 7) - 1;

    private WorkspaceStaffOnboardingCorrectionReceipt() { }

    private WorkspaceStaffOnboardingCorrectionReceipt(
        Guid id,
        string scopeId)
        : base(id, scopeId)
    {
    }

    public int ContractVersion { get; private set; }
    public Guid ExecutionId { get; private set; }
    public Guid CaseId { get; private set; }
    public long ApprovalRevision { get; private set; }
    public Guid ApplicationId { get; private set; }
    public long SelectedRecordVersion { get; private set; }
    public long CurrentRecordVersion { get; private set; }
    public int ChangedFieldsMask { get; private set; }
    public string RequestSha256 { get; private set; } = string.Empty;
    public Guid ApplicantEventId { get; private set; }
    public Guid CompletionEventId { get; private set; }
    public DateTimeOffset CompletedAtUtc { get; private set; }

    public IReadOnlyCollection<WorkspaceStaffOnboardingApplicantField>
        ChangedFields =>
        Enum.GetValues<WorkspaceStaffOnboardingApplicantField>()
            .Where(candidate =>
                candidate is not WorkspaceStaffOnboardingApplicantField.Unknown &&
                (this.ChangedFieldsMask & ToMask(candidate)) != 0)
            .ToArray();

    public static Result<WorkspaceStaffOnboardingCorrectionReceipt> Create(
        Guid receiptId,
        string tenantId,
        Guid executionId,
        Guid caseId,
        long approvalRevision,
        Guid applicationId,
        long selectedRecordVersion,
        long currentRecordVersion,
        IReadOnlyCollection<WorkspaceStaffOnboardingApplicantField>
            changedFields,
        string requestSha256,
        Guid applicantEventId,
        Guid completionEventId,
        DateTimeOffset completedAtUtc)
    {
        string digest = NormalizeDigest(requestSha256);
        if (receiptId == Guid.Empty ||
            executionId == Guid.Empty ||
            caseId == Guid.Empty ||
            applicationId == Guid.Empty ||
            applicantEventId == Guid.Empty ||
            completionEventId == Guid.Empty ||
            completionEventId == applicantEventId ||
            digest.Length != DigestLength ||
            completedAtUtc == default ||
            !TenantIds.TryNormalize(tenantId, out string? scopeId))
        {
            return Result.Failure<WorkspaceStaffOnboardingCorrectionReceipt>(
                WorkspaceStaffOnboardingErrors.CorrectionReceiptIdentityInvalid);
        }

        if (approvalRevision < 1 ||
            selectedRecordVersion < 1 ||
            currentRecordVersion != selectedRecordVersion + 1)
        {
            return Result.Failure<WorkspaceStaffOnboardingCorrectionReceipt>(
                WorkspaceStaffOnboardingErrors.CorrectionReceiptVersionInvalid);
        }

        int changedFieldsMask = ToMask(changedFields);
        if (changedFieldsMask is <= 0 or > AllChangedFieldsMask)
        {
            return Result.Failure<WorkspaceStaffOnboardingCorrectionReceipt>(
                WorkspaceStaffOnboardingErrors.CorrectionReceiptFieldsInvalid);
        }

        WorkspaceStaffOnboardingCorrectionReceipt receipt =
            new(receiptId, scopeId)
            {
                ContractVersion = CurrentContractVersion,
                ExecutionId = executionId,
                CaseId = caseId,
                ApprovalRevision = approvalRevision,
                ApplicationId = applicationId,
                SelectedRecordVersion = selectedRecordVersion,
                CurrentRecordVersion = currentRecordVersion,
                ChangedFieldsMask = changedFieldsMask,
                RequestSha256 = digest,
                ApplicantEventId = applicantEventId,
                CompletionEventId = completionEventId,
                CompletedAtUtc = completedAtUtc
            };
        receipt.RaiseDomainEvent(
            new WorkspaceStaffOnboardingCorrectionAppliedDomainEvent(
                completionEventId,
                completedAtUtc,
                scopeId,
                executionId,
                receiptId,
                caseId,
                approvalRevision,
                applicationId,
                selectedRecordVersion,
                currentRecordVersion,
                changedFields));
        return Result.Success(receipt);
    }

    public bool MatchesReplay(
        Guid caseId,
        long approvalRevision,
        Guid applicationId,
        long selectedRecordVersion,
        string requestSha256) =>
        this.CaseId == caseId &&
        this.ApprovalRevision == approvalRevision &&
        this.ApplicationId == applicationId &&
        this.SelectedRecordVersion == selectedRecordVersion &&
        string.Equals(
            this.RequestSha256,
            NormalizeDigest(requestSha256),
            StringComparison.Ordinal);

    private static int ToMask(
        IReadOnlyCollection<WorkspaceStaffOnboardingApplicantField> fields)
    {
        if (fields is null ||
            fields.Count == 0 ||
            fields.Any(candidate =>
                candidate is WorkspaceStaffOnboardingApplicantField.Unknown ||
                !Enum.IsDefined(candidate)))
        {
            return 0;
        }

        return fields.Aggregate(
            0,
            (mask, candidate) => mask | ToMask(candidate));
    }

    private static int ToMask(
        WorkspaceStaffOnboardingApplicantField candidate) =>
        1 << ((int)candidate - 1);

    private static string NormalizeDigest(string value)
    {
        string normalized =
            value?.Trim().ToLowerInvariant() ?? string.Empty;
        return normalized.Length == DigestLength &&
            normalized.All(Uri.IsHexDigit)
                ? normalized
                : string.Empty;
    }
}
