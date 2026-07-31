namespace BunkFy.Extensions.Operations.Notifications;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Gma.Modules.Notifications.Application.Ports;
using Gma.Modules.Notifications.Contracts;

internal static class OperationsNotificationsDataRightsReceipt
{
    public const int ContractVersion = 1;
    public const int MaximumRecords = 1_000;
    public const int StaffMaximumRecords =
        NotificationHistoryLifecycleLimits.MaximumCloseRecords;
    public const string DispositionCode =
        "operations-notifications.completed";
    public const string ReasonCode =
        "operations-notifications.reservation-history-removed";
    public const string StaffReasonCode =
        "operations-notifications.staff-inbox-history-removed";

    public static bool IsValid(
        NotificationHistoryReferenceCloseReceipt? receipt,
        Guid operationId,
        NotificationHistoryReference reference,
        long expectedVersion) =>
        receipt is not null &&
        receipt.OperationId == operationId &&
        receipt.Reference == reference &&
        expectedVersion < long.MaxValue &&
        receipt.ResultingVersion == expectedVersion + 1 &&
        receipt.RemovedRecordCount >= 0 &&
        OperationsNotificationsDataRightsValidation.IsSha256(
            receipt.RemovedRecordIdsSha256) &&
        receipt.CompletedAtUtc != default;

    public static string ComputeSha256(
        NotificationHistoryReferenceCloseReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        string canonical = string.Join(
            '|',
            "bunkfy-operations-notification-history-close-receipt/v1",
            receipt.OperationId.ToString("N"),
            receipt.Reference.Namespace,
            receipt.Reference.Digest,
            receipt.ResultingVersion.ToString(
                CultureInfo.InvariantCulture),
            receipt.RemovedRecordCount.ToString(
                CultureInfo.InvariantCulture),
            receipt.RemovedRecordIdsSha256);
        return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
    }
}
