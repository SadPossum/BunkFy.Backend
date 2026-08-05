namespace BunkFy.Extensions.DataRights.TenantTermination;

public sealed class TenantTerminationProductionAdmissionOptions
{
    public const string SectionName =
        "BunkFy:TenantTermination:ProductionAdmission";

    public bool ExecutionEnabled { get; set; }
    public TenantTerminationApprovalState ApprovalState { get; set; }
    public string? ApprovalReference { get; set; }
    public string? OwnerCatalogSha256 { get; set; }
    public string? BackupEvidenceReference { get; set; }
    public string? RestoreDrillEvidenceReference { get; set; }
    public string? OperatorAssuranceReference { get; set; }
}

public enum TenantTerminationApprovalState
{
    Pending = 0,
    Approved = 1
}
