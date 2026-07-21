namespace BunkFy.Modules.Workspaces.Domain;

public sealed class WorkspaceStaffAccessPlanProperty
{
    private WorkspaceStaffAccessPlanProperty() { }

    internal WorkspaceStaffAccessPlanProperty(string scopeId, Guid planId, Guid propertyId)
    {
        this.ScopeId = scopeId;
        this.PlanId = planId;
        this.PropertyId = propertyId;
    }

    public string ScopeId { get; private set; } = string.Empty;
    public Guid PlanId { get; private set; }
    public Guid PropertyId { get; private set; }
}
