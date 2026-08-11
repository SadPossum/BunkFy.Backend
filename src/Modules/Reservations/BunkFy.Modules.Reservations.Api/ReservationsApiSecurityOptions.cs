namespace BunkFy.Modules.Reservations.Api;

using Gma.Framework.Security;

public sealed class ReservationsApiSecurityOptions
{
    public AuthenticationAssuranceRequirement? CorrectionExecutionAssurance { get; set; }
}
