namespace BunkFy.Modules.Inventory.Api;

using Gma.Framework.Security;

public sealed class InventoryApiSecurityOptions
{
    public AuthenticationAssuranceRequirement? TopologyRetirementAssurance { get; set; }
}
