namespace BunkFy.Modules.Properties.Application.Handlers;

using BunkFy.DataGovernance;
using BunkFy.Modules.Properties.Application.Queries;
using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class ListCountryPoliciesQueryHandler(CountryPolicyRegistry registry)
    : IQueryHandler<ListCountryPoliciesQuery, CountryPolicyListResponse>
{
    public Task<Result<CountryPolicyListResponse>> HandleAsync(
        ListCountryPoliciesQuery query,
        CancellationToken cancellationToken)
    {
        CountryPolicyDescriptorDto[] policies = registry.ListPolicies()
            .Select(policy => new CountryPolicyDescriptorDto(
                policy.PolicyId,
                policy.PolicyVersion,
                policy.OperatingCountryCode,
                ToWireName(policy.LaunchStatus),
                ToWireName(policy.ApprovalState),
                policy.EffectiveAtUtc,
                policy.ExpiresAtUtc,
                policy.ContentSha256,
                policy.AccommodationTypes,
                policy.PermittedDataRegions,
                policy.PermittedTransferProfiles,
                policy.RetentionPolicies.Select(retention =>
                    new CountryPolicyRetentionDescriptorDto(
                        retention.RetentionPolicyId,
                        retention.RetentionPolicyVersion)).ToArray(),
                policy.RequiredAcknowledgements.Select(acknowledgement =>
                    new PropertyGovernanceAcknowledgementDto(
                        acknowledgement.AcknowledgementId,
                        acknowledgement.AcknowledgementVersion)).ToArray()))
            .ToArray();

        return Task.FromResult(Result.Success(new CountryPolicyListResponse(policies)));
    }

    private static string ToWireName(CountryLaunchStatus status) =>
        status switch
        {
            CountryLaunchStatus.Disabled => "disabled",
            CountryLaunchStatus.Engineering => "engineering",
            CountryLaunchStatus.Approved => "approved",
            _ => "unknown"
        };

    private static string ToWireName(CountryPolicyApprovalState status) =>
        status switch
        {
            CountryPolicyApprovalState.Example => "example",
            CountryPolicyApprovalState.Approved => "approved",
            _ => "unknown"
        };
}
