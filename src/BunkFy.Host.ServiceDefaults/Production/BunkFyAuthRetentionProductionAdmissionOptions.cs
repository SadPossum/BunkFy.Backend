namespace BunkFy.Host.ServiceDefaults.Production;

public sealed class BunkFyAuthRetentionProductionAdmissionOptions
{
    public const string SectionName =
        "BunkFy:AuthRetention:ProductionAdmission";

    public BunkFyAuthRetentionApprovalState ApprovalState { get; set; }

    public string? ApprovalReference { get; set; }

    public BunkFyAuthRetentionMaintenanceOwner MaintenanceOwner { get; set; }

    public int MaintenanceOwnerInstanceCount { get; set; }

    public bool CurrentProcessOwnsMaintenance { get; set; }

    public BunkFyExistingHistoryDisposition ExistingHistoryDisposition { get; set; }

    public int ExpiredExchangeHistoryHours { get; set; } = 24;

    public int PasswordRecoveryHistoryHours { get; set; } = 24;

    public int SessionHistoryDays { get; set; } = 365;

    public int AuthenticationChallengeHistoryHours { get; set; } = 24;

    public int ExpiredTotpEnrollmentHistoryHours { get; set; } = 24;

    public int DisabledTotpAuthenticatorHistoryDays { get; set; } = 365;

    public int MultiFactorFailureHistoryHours { get; set; } = 24;

    public int AuthenticationFailureHistoryHours { get; set; } = 24;
}

public enum BunkFyAuthRetentionApprovalState
{
    Pending = 0,
    Approved = 1
}

public enum BunkFyAuthRetentionMaintenanceOwner
{
    Unspecified = 0,
    Worker = 1
}
