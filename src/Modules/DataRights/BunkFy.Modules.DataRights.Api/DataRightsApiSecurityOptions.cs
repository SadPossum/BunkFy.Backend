namespace BunkFy.Modules.DataRights.Api;

using Gma.Framework.Security;

public sealed class DataRightsApiSecurityOptions
{
    public AuthenticationAssuranceRequirement? AnonymisationExecutionAssurance { get; set; }
}
