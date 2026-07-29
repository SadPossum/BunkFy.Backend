namespace BunkFy.Modules.Staff.Api;

using Gma.Framework.Security;

public sealed class StaffApiSecurityOptions
{
    public AuthenticationAssuranceRequirement? EmploymentGovernanceAssurance { get; set; }

    public AuthenticationAssuranceRequirement? DataHoldReleaseAssurance { get; set; }
}
