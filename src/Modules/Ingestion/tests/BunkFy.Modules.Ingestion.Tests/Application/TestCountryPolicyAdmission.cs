namespace BunkFy.Modules.Ingestion.Tests.Application;

using BunkFy.DataGovernance;
using BunkFy.Modules.Ingestion.Application.Ports;

internal sealed class TestCountryPolicyAdmission(
    bool allowed = true,
    CountryPolicyDecisionReason denialReason = CountryPolicyDecisionReason.MissingBinding)
    : IIngestionCountryPolicyAdmission
{
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);

    public Task<CountryPolicyDecision> EvaluateAsync(
        Guid propertyId,
        string purposeCode,
        CountryPolicySurface surface,
        string sourceProvenance,
        CancellationToken cancellationToken) =>
        Task.FromResult(allowed
            ? CountryPolicyDecision.Allow(new CountryPolicyEvidence(
                "GB",
                "gb-hostel",
                1,
                "eu-west",
                "none",
                "guest-standard",
                1,
                new string('a', 64),
                purposeCode,
                surface,
                sourceProvenance,
                CountryPolicyApprovalState.Approved,
                Now.AddDays(-1),
                Now.AddDays(30),
                Now,
                []))
            : CountryPolicyDecision.Deny(denialReason));
}
