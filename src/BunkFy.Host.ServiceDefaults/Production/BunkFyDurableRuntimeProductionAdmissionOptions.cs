namespace BunkFy.Host.ServiceDefaults.Production;

public sealed class BunkFyDurableRuntimeProductionAdmissionOptions
{
    public const string SectionName =
        "BunkFy:DurableRuntime:ProductionAdmission";

    public BunkFyDurableRuntimeApprovalState ApprovalState { get; set; }

    public string? ApprovalReference { get; set; }

    public BunkFyDurableRuntimeMaintenanceOwner MaintenanceOwner { get; set; }

    public int MaintenanceOwnerInstanceCount { get; set; }

    public bool CurrentProcessOwnsMaintenance { get; set; }

    public TimeSpan ProcessedOutboxRetention { get; set; } =
        TimeSpan.FromDays(7);

    public TimeSpan ProcessedInboxRetention { get; set; } =
        TimeSpan.FromDays(14);

    public TimeSpan BrokerReplayHorizon { get; set; } =
        TimeSpan.FromDays(7);

    public TimeSpan SucceededRunRetention { get; set; } =
        TimeSpan.FromDays(30);

    public TimeSpan FailedRunRetention { get; set; } =
        TimeSpan.FromDays(90);

    public TimeSpan CanceledRunRetention { get; set; } =
        TimeSpan.FromDays(30);

    public TimeSpan TimedOutRunRetention { get; set; } =
        TimeSpan.FromDays(90);

    public TimeSpan HandledControlRetention { get; set; } =
        TimeSpan.FromDays(30);

    public TimeSpan FailedControlRetention { get; set; } =
        TimeSpan.FromDays(90);

    public TimeSpan ExpiredControlRetention { get; set; } =
        TimeSpan.FromDays(30);
}

public enum BunkFyDurableRuntimeApprovalState
{
    Pending = 0,
    Approved = 1
}

public enum BunkFyDurableRuntimeMaintenanceOwner
{
    Unspecified = 0,
    Worker = 1
}

public enum BunkFyDurableRuntimeHostRole
{
    PublicApi = 1,
    AdminApi = 2,
    Worker = 3
}
