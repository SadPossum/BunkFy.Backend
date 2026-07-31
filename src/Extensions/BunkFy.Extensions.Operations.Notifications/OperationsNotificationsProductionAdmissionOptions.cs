namespace BunkFy.Extensions.Operations.Notifications;

public sealed class OperationsNotificationsProductionAdmissionOptions
{
    public const string SectionName =
        "BunkFy:OperationsNotifications:ProductionAdmission";

    public OperationsNotificationsApprovalState ApprovalState { get; set; }

    public string? ApprovalReference { get; set; }

    public int CatalogVersion { get; set; }

    public string? CatalogSha256 { get; set; }

    public int ReadHistoryDays { get; set; } = 90;

    public int UnreadHistoryDays { get; set; } = 365;

    public int BroadcastDays { get; set; } = 365;

    public int DeliveryAttemptDays { get; set; } = 90;

    public OperationsNotificationsLegacyHistoryDisposition LegacyHistoryDisposition
    {
        get;
        set;
    }

    public OperationsNotificationsRetentionOwner RetentionOwner { get; set; }

    public int RetentionOwnerInstanceCount { get; set; }
}

public enum OperationsNotificationsApprovalState
{
    Pending = 0,
    Approved = 1
}

public enum OperationsNotificationsLegacyHistoryDisposition
{
    Unspecified = 0,
    ResetBeforeAdmission = 1,
    VerifiedReferenceComplete = 2
}

public enum OperationsNotificationsRetentionOwner
{
    Unspecified = 0,
    PublicApi = 1,
    Worker = 2
}

public enum OperationsNotificationsProductionHostRole
{
    PublicApi = 1,
    AdminApi = 2,
    Worker = 3
}
