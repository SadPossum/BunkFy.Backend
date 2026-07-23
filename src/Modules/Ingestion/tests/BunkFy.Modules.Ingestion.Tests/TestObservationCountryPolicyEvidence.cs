namespace BunkFy.Modules.Ingestion.Tests;

using BunkFy.Modules.Ingestion.Domain.Receipts;

internal static class TestObservationCountryPolicyEvidence
{
    public static ObservationCountryPolicyEvidence Create(DateTimeOffset evaluatedAtUtc) =>
        ObservationCountryPolicyEvidence.Create(
            "GB",
            "gb-hostel",
            1,
            "eu-west",
            "none",
            "guest-standard",
            1,
            new string('a', ObservationCountryPolicyEvidence.ContentSha256Length),
            "reservation-ingestion",
            "adapter-ingress",
            "approved-adapter",
            evaluatedAtUtc.AddDays(-1),
            evaluatedAtUtc.AddDays(30),
            evaluatedAtUtc).Value;
}
