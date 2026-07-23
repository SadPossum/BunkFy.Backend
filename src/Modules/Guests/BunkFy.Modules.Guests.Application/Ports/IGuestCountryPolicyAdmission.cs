namespace BunkFy.Modules.Guests.Application.Ports;

using BunkFy.DataGovernance;

public interface IGuestCountryPolicyAdmission
{
    Task<CountryPolicyDecision> EvaluateAsync(
        Guid propertyId,
        string purposeCode,
        CountryPolicySurface surface,
        string sourceProvenance,
        CancellationToken cancellationToken);
}
