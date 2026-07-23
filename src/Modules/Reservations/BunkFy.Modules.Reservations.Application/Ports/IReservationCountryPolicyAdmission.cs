namespace BunkFy.Modules.Reservations.Application.Ports;

using BunkFy.DataGovernance;

public interface IReservationCountryPolicyAdmission
{
    Task<CountryPolicyDecision> EvaluateAsync(
        Guid propertyId,
        string purposeCode,
        CountryPolicySurface surface,
        string sourceProvenance,
        CancellationToken cancellationToken);
}
