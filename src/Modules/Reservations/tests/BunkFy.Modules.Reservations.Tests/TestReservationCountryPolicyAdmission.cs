namespace BunkFy.Modules.Reservations.Tests;

using BunkFy.DataGovernance;
using BunkFy.Modules.Reservations.Application.Ports;

internal sealed class TestReservationCountryPolicyAdmission(
    bool allowed = true,
    CountryPolicyDecisionReason denialReason = CountryPolicyDecisionReason.MissingBinding)
    : IReservationCountryPolicyAdmission
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
                "eu-west-2",
                "uk-no-transfer",
                "guest-operational",
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
