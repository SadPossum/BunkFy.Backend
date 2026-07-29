namespace BunkFy.Modules.Staff.Domain.DataRights;

using BunkFy.Modules.Staff.Domain.Errors;
using BunkFy.Modules.Staff.Domain.Events;
using BunkFy.Modules.Staff.Domain.Models;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class StaffDataRightsCorrectionReceipt : ScopedAggregateRoot<Guid>
{
    public const int CurrentContractVersion = 1;
    public const int DigestLength = 64;
    public const int AllChangedFieldsMask = (1 << 7) - 1;

    private StaffDataRightsCorrectionReceipt() { }

    private StaffDataRightsCorrectionReceipt(Guid id, string scopeId)
        : base(id, scopeId)
    {
    }

    public int ContractVersion { get; private set; }
    public Guid ExecutionId { get; private set; }
    public Guid CaseId { get; private set; }
    public long ApprovalRevision { get; private set; }
    public Guid StaffMemberId { get; private set; }
    public long SelectedRecordVersion { get; private set; }
    public long CurrentRecordVersion { get; private set; }
    public int ChangedFieldsMask { get; private set; }
    public string RequestSha256 { get; private set; } = string.Empty;
    public Guid ProfileEventId { get; private set; }
    public Guid CompletionEventId { get; private set; }
    public DateTimeOffset CompletedAtUtc { get; private set; }

    public IReadOnlyCollection<StaffProfileField> ChangedFields =>
        Enum.GetValues<StaffProfileField>()
            .Where(candidate =>
                candidate is not StaffProfileField.Unknown &&
                (this.ChangedFieldsMask & ToMask(candidate)) != 0)
            .ToArray();

    public static Result<StaffDataRightsCorrectionReceipt> Create(
        Guid receiptId,
        string tenantId,
        Guid executionId,
        Guid caseId,
        long approvalRevision,
        Guid staffMemberId,
        long selectedRecordVersion,
        long currentRecordVersion,
        IReadOnlyCollection<StaffProfileField> changedFields,
        string requestSha256,
        Guid profileEventId,
        Guid completionEventId,
        DateTimeOffset completedAtUtc)
    {
        string digest = NormalizeDigest(requestSha256);
        if (receiptId == Guid.Empty ||
            executionId == Guid.Empty ||
            caseId == Guid.Empty ||
            staffMemberId == Guid.Empty ||
            profileEventId == Guid.Empty ||
            completionEventId == Guid.Empty ||
            completionEventId == profileEventId ||
            digest.Length != DigestLength ||
            completedAtUtc == default ||
            !TenantIds.TryNormalize(tenantId, out string? scopeId))
        {
            return Result.Failure<StaffDataRightsCorrectionReceipt>(
                StaffDomainErrors.CorrectionReceiptIdentityInvalid);
        }

        if (approvalRevision < 1 ||
            selectedRecordVersion < 1 ||
            currentRecordVersion != selectedRecordVersion + 1)
        {
            return Result.Failure<StaffDataRightsCorrectionReceipt>(
                StaffDomainErrors.CorrectionReceiptVersionInvalid);
        }

        int changedFieldsMask = ToMask(changedFields);
        if (changedFieldsMask is <= 0 or > AllChangedFieldsMask)
        {
            return Result.Failure<StaffDataRightsCorrectionReceipt>(
                StaffDomainErrors.CorrectionReceiptFieldsInvalid);
        }

        StaffDataRightsCorrectionReceipt receipt = new(receiptId, scopeId)
        {
            ContractVersion = CurrentContractVersion,
            ExecutionId = executionId,
            CaseId = caseId,
            ApprovalRevision = approvalRevision,
            StaffMemberId = staffMemberId,
            SelectedRecordVersion = selectedRecordVersion,
            CurrentRecordVersion = currentRecordVersion,
            ChangedFieldsMask = changedFieldsMask,
            RequestSha256 = digest,
            ProfileEventId = profileEventId,
            CompletionEventId = completionEventId,
            CompletedAtUtc = completedAtUtc
        };
        receipt.RaiseDomainEvent(new StaffDataRightsCorrectionAppliedDomainEvent(
            completionEventId,
            completedAtUtc,
            scopeId,
            executionId,
            receiptId,
            caseId,
            approvalRevision,
            staffMemberId,
            selectedRecordVersion,
            currentRecordVersion,
            changedFields));
        return Result.Success(receipt);
    }

    public bool MatchesReplay(
        Guid caseId,
        long approvalRevision,
        Guid staffMemberId,
        long selectedRecordVersion,
        string requestSha256) =>
        this.CaseId == caseId &&
        this.ApprovalRevision == approvalRevision &&
        this.StaffMemberId == staffMemberId &&
        this.SelectedRecordVersion == selectedRecordVersion &&
        string.Equals(
            this.RequestSha256,
            NormalizeDigest(requestSha256),
            StringComparison.Ordinal);

    private static int ToMask(IReadOnlyCollection<StaffProfileField> fields)
    {
        if (fields is null || fields.Count == 0 || fields.Any(candidate =>
                candidate is StaffProfileField.Unknown || !Enum.IsDefined(candidate)))
        {
            return 0;
        }

        return fields.Aggregate(0, (mask, candidate) => mask | ToMask(candidate));
    }

    private static int ToMask(StaffProfileField candidate) =>
        1 << ((int)candidate - 1);

    private static string NormalizeDigest(string value)
    {
        string normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;
        return normalized.Length == DigestLength && normalized.All(Uri.IsHexDigit)
            ? normalized
            : string.Empty;
    }
}
