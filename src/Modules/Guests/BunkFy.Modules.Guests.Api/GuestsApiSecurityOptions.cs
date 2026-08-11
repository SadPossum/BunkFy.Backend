namespace BunkFy.Modules.Guests.Api;

using Gma.Framework.Security;

public sealed class GuestsApiSecurityOptions
{
    public AuthenticationAssuranceRequirement? CorrectionExecutionAssurance { get; set; }

    public AuthenticationAssuranceRequirement? RestrictionExecutionAssurance { get; set; }

    public AuthenticationAssuranceRequirement? DataHoldReleaseAssurance { get; set; }
}
