namespace BunkFy.Modules.Ingestion.Application.Ports;

using BunkFy.DataGovernance;

public interface IIngestionCountryPolicyAdmission
{
    Task<CountryPolicyDecision> EvaluateAsync(
        Guid propertyId,
        string purposeCode,
        CountryPolicySurface surface,
        string sourceProvenance,
        CancellationToken cancellationToken);
}
