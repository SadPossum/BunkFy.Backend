namespace BunkFy.Host.ServiceDefaults.Production;

public sealed class BunkFyOrganizationsMaintenanceProductionAdmissionOptions
{
    public const string SectionName =
        "BunkFy:OrganizationsMaintenance:ProductionAdmission";

    public BunkFyOrganizationsMaintenanceApprovalState ApprovalState { get; set; }

    public string? ApprovalReference { get; set; }

    public BunkFyOrganizationsMaintenanceOwner MaintenanceOwner { get; set; }

    public int MaintenanceOwnerInstanceCount { get; set; }

    public bool CurrentProcessOwnsMaintenance { get; set; }

    public BunkFyExistingHistoryDisposition ExistingHistoryDisposition { get; set; }

    public int InvitationHistoryDays { get; set; } = 90;

    public int EnrollmentHistoryDays { get; set; } = 90;
}

public enum BunkFyOrganizationsMaintenanceApprovalState
{
    Pending = 0,
    Approved = 1
}

public enum BunkFyOrganizationsMaintenanceOwner
{
    Unspecified = 0,
    Worker = 1
}
