namespace BunkFy.Modules.DataRights.Api;

using Gma.Framework.Security;

public sealed class DataRightsApiSecurityOptions
{
    public AuthenticationAssuranceRequirement? AnonymisationExecutionAssurance { get; set; }
    public AuthenticationAssuranceRequirement? RestrictionExecutionAssurance { get; set; }
    public AuthenticationAssuranceRequirement? ExportGenerationAssurance { get; set; }
    public AuthenticationAssuranceRequirement? ExportDownloadAssurance { get; set; }
}
