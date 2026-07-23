namespace BunkFy.Modules.Properties.Contracts;

public sealed record PropertyGovernancePolicyBinding
{
    public PropertyGovernancePolicyBinding(
        string operatingCountryCode,
        string policyId,
        int policyVersion,
        string dataRegionId,
        string transferProfileId,
        string retentionPolicyId,
        int retentionPolicyVersion,
        string contentSha256,
        DateTimeOffset policyEffectiveAtUtc,
        DateTimeOffset policyExpiresAtUtc,
        DateTimeOffset activatedAtUtc,
        IReadOnlyCollection<PropertyGovernanceAcknowledgement> acknowledgements)
    {
        this.OperatingCountryCode = PropertiesEventContractGuards.RequireCountryCode(
            operatingCountryCode,
            nameof(operatingCountryCode));
        this.PolicyId = PropertiesEventContractGuards.RequirePolicyKey(policyId, nameof(policyId));
        this.PolicyVersion = PropertiesEventContractGuards.RequireVersion(policyVersion, nameof(policyVersion));
        this.DataRegionId = PropertiesEventContractGuards.RequirePolicyKey(dataRegionId, nameof(dataRegionId));
        this.TransferProfileId = PropertiesEventContractGuards.RequirePolicyKey(
            transferProfileId,
            nameof(transferProfileId));
        this.RetentionPolicyId = PropertiesEventContractGuards.RequirePolicyKey(
            retentionPolicyId,
            nameof(retentionPolicyId));
        this.RetentionPolicyVersion = PropertiesEventContractGuards.RequireVersion(
            retentionPolicyVersion,
            nameof(retentionPolicyVersion));
        this.ContentSha256 = PropertiesEventContractGuards.RequireSha256(contentSha256, nameof(contentSha256));
        if (policyEffectiveAtUtc == default || policyExpiresAtUtc <= policyEffectiveAtUtc ||
            activatedAtUtc < policyEffectiveAtUtc || activatedAtUtc >= policyExpiresAtUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(policyExpiresAtUtc));
        }

        this.PolicyEffectiveAtUtc = policyEffectiveAtUtc;
        this.PolicyExpiresAtUtc = policyExpiresAtUtc;
        this.ActivatedAtUtc = activatedAtUtc;
        this.Acknowledgements = PropertiesEventContractGuards.RequireAcknowledgements(acknowledgements);
    }

    public string OperatingCountryCode { get; }
    public string PolicyId { get; }
    public int PolicyVersion { get; }
    public string DataRegionId { get; }
    public string TransferProfileId { get; }
    public string RetentionPolicyId { get; }
    public int RetentionPolicyVersion { get; }
    public string ContentSha256 { get; }
    public DateTimeOffset PolicyEffectiveAtUtc { get; }
    public DateTimeOffset PolicyExpiresAtUtc { get; }
    public DateTimeOffset ActivatedAtUtc { get; }
    public IReadOnlyCollection<PropertyGovernanceAcknowledgement> Acknowledgements { get; }
}

public sealed record PropertyGovernanceAcknowledgement
{
    public PropertyGovernanceAcknowledgement(string acknowledgementId, int acknowledgementVersion)
    {
        this.AcknowledgementId = PropertiesEventContractGuards.RequirePolicyKey(
            acknowledgementId,
            nameof(acknowledgementId));
        this.AcknowledgementVersion = PropertiesEventContractGuards.RequireVersion(
            acknowledgementVersion,
            nameof(acknowledgementVersion));
    }

    public string AcknowledgementId { get; }
    public int AcknowledgementVersion { get; }
}
