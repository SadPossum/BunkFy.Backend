namespace BunkFy.Modules.Staff.Application.Contributors;

using BunkFy.DataGovernance;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Domain.DataRights;
using BunkFy.Modules.Staff.Domain.Governance;
using BunkFy.Modules.Staff.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;

internal sealed class StaffDataRightsAnonymisationPolicyContributor(
    IStaffMemberRepository members,
    IStaffEmploymentGovernanceRepository governanceRepository,
    IStaffProcessingRestrictionProjectionRepository restrictionProjections,
    IStaffDataHoldRepository holds,
    IStaffOperationLock operationLock,
    CountryPolicyRegistry countryPolicies,
    IScopeContext scopeContext,
    ISystemClock clock)
    : IDataRightsAnonymisationPolicyContributor
{
    internal const string AccommodationType =
        StaffRetentionCoordinates.AccommodationType;
    internal const string PurposeCode =
        "staff-data-rights-anonymisation";
    internal const string RetentionPurposeCode =
        StaffRetentionCoordinates.Purpose;
    internal const string Surface = "erasure";
    internal const string SourceProvenance =
        "authorized-workspace-operator";
    internal const string RetentionSourceProvenance =
        StaffRetentionCoordinates.SourceProvenance;
    internal const string RetentionDataClass =
        StaffRetentionCoordinates.DataClassKey;
    internal const string RetentionTrigger =
        StaffRetentionCoordinates.Trigger;

    private const string InvalidRequestCode =
        "staff.anonymisation-policy.invalid-request";
    private const string StateUnavailableCode =
        "staff.anonymisation-policy.state-unavailable";
    private const string LifecycleDeniedCode =
        "staff.anonymisation-policy.lifecycle-denied";
    private const string AssignmentActiveCode =
        "staff.anonymisation-policy.assignment-active";
    private const string GovernanceDeniedCode =
        "staff.anonymisation-policy.governance-denied";
    private const string ActiveHoldCode =
        "staff.anonymisation-policy.active-hold";
    private const string RetentionNotDueCode =
        "staff.anonymisation-policy.retention-not-due";
    private const string ConcurrentChangeCode =
        "staff.anonymisation-policy.concurrent-change";

    public int ContractVersion =>
        DataRightsAnonymisationPolicyContract.CurrentVersion;

    public DataRightsCaseType CaseType =>
        DataRightsCaseType.StaffRights;

    public string OwnerKey => StaffDataRightsCoordinates.Owner;

    public string RecordType =>
        StaffDataRightsCoordinates.StaffMemberRecordType;

    public async Task<DataRightsAnonymisationPolicyContributionResult>
        EvaluateAsync(
            DataRightsAnonymisationPolicyContributionRequest request,
            CancellationToken cancellationToken)
    {
        if (!this.IsValidRequest(request))
        {
            return Denied(InvalidRequestCode);
        }

        long? lockRevisionBefore =
            await operationLock.GetStaffMemberRevisionAsync(
                request.TenantId,
                request.Coordinate.RecordId,
                cancellationToken).ConfigureAwait(false);
        if (lockRevisionBefore is null or < 1)
        {
            return Denied(StateUnavailableCode);
        }

        StaffMember? member = await members.GetForDataRightsAsync(
            request.Coordinate.RecordId,
            cancellationToken).ConfigureAwait(false);
        if (member is null ||
            !string.Equals(
                member.ScopeId,
                request.TenantId,
                StringComparison.Ordinal))
        {
            return Denied(StateUnavailableCode);
        }

        if (member.Version != request.Coordinate.RecordVersion ||
            member.Status != StaffMemberState.Departed ||
            member.DepartedAtUtc is null)
        {
            return Denied(LifecycleDeniedCode);
        }

        if (member.Assignments.Any(assignment => assignment.IsCurrent))
        {
            return Denied(AssignmentActiveCode);
        }

        StaffEmploymentGovernance? governance =
            await governanceRepository.GetAsync(
                member.Id,
                cancellationToken).ConfigureAwait(false);
        StaffProcessingRestrictionProjection? restriction =
            await restrictionProjections.GetAsync(
                member.Id,
                cancellationToken).ConfigureAwait(false);
        if (governance is null ||
            restriction is null ||
            !string.Equals(
                governance.ScopeId,
                request.TenantId,
                StringComparison.Ordinal) ||
            governance.StaffMemberId != member.Id ||
            governance.SelectedStaffVersion != member.Version ||
            !string.Equals(
                restriction.ScopeId,
                request.TenantId,
                StringComparison.Ordinal) ||
            restriction.StaffMemberId != member.Id ||
            restriction.ContractVersion !=
                StaffProcessingRestrictionContract.CurrentVersion)
        {
            return Denied(StateUnavailableCode);
        }

        long activeHoldCount = await holds.CountAsync(
            member.Id,
            StaffDataHoldStatus.Active,
            cancellationToken).ConfigureAwait(false);
        if (activeHoldCount != 0)
        {
            return Denied(ActiveHoldCode);
        }

        IReadOnlyCollection<StaffDataHold>? holdSnapshot =
            await StaffDataHoldSnapshotReader.ReadAsync(
                request.TenantId,
                member.Id,
                holds,
                cancellationToken).ConfigureAwait(false);
        if (holdSnapshot is null)
        {
            return Denied(StateUnavailableCode);
        }

        DateTimeOffset nowUtc = clock.UtcNow.ToUniversalTime();
        CountryPolicyBinding policyBinding = ToPolicyBinding(governance);
        CountryPolicyDecision operation = countryPolicies.EvaluateOperation(
            new(
                policyBinding,
                AccommodationType,
                PurposeCode,
                CountryPolicySurface.Erasure,
                SourceProvenance,
                nowUtc));
        CountryPolicyRetentionDecision retention =
            countryPolicies.EvaluateRetention(
                new(
                    policyBinding,
                    AccommodationType,
                    RetentionPurposeCode,
                    RetentionSourceProvenance,
                    RetentionDataClass,
                    RetentionTrigger,
                    nowUtc));
        if (!operation.IsAllowed ||
            operation.Evidence is null ||
            !retention.IsAllowed ||
            retention.Evidence is null ||
            retention.RetentionRule is null ||
            !MatchesPolicy(operation.Evidence, retention.Evidence))
        {
            return Denied(GovernanceDeniedCode);
        }

        DateTimeOffset triggeredAtUtc =
            member.DepartedAtUtc.Value.ToUniversalTime();
        DateTimeOffset retentionDeadlineUtc;
        try
        {
            retentionDeadlineUtc =
                triggeredAtUtc.Add(retention.RetentionRule.Period);
        }
        catch (ArgumentOutOfRangeException)
        {
            return Denied(GovernanceDeniedCode);
        }

        if (retentionDeadlineUtc > nowUtc)
        {
            return Denied(RetentionNotDueCode);
        }

        long? lockRevisionAfter =
            await operationLock.GetStaffMemberRevisionAsync(
                request.TenantId,
                request.Coordinate.RecordId,
                cancellationToken).ConfigureAwait(false);
        if (lockRevisionAfter is null or < 1 ||
            lockRevisionAfter != lockRevisionBefore)
        {
            return Denied(ConcurrentChangeCode);
        }

        CountryPolicyEvidence evidence = operation.Evidence;
        return DataRightsAnonymisationPolicyContributionResult.Approved(
            new(
                evidence.OperatingCountryCode,
                evidence.PolicyId,
                evidence.PolicyVersion,
                evidence.RetentionPolicyId,
                evidence.RetentionPolicyVersion,
                evidence.ContentSha256,
                evidence.PurposeCode,
                Surface,
                evidence.SourceProvenance,
                retention.RetentionRule.DataClass,
                retention.RetentionRule.Trigger,
                triggeredAtUtc,
                retentionDeadlineUtc,
                nowUtc,
                StaffAnonymisationPolicyEvidence.CreateBindings(
                    member,
                    governance,
                    restriction,
                    holdSnapshot,
                    lockRevisionAfter.Value),
                RequiresDistinctExecutor: true));
    }

    private bool IsValidRequest(
        DataRightsAnonymisationPolicyContributionRequest? request) =>
        request is not null &&
        request.ContractVersion ==
            DataRightsAnonymisationPolicyContract.CurrentVersion &&
        request.CaseType == DataRightsCaseType.StaffRights &&
        request.PropertyId is null &&
        request.CaseId != Guid.Empty &&
        TenantIds.TryNormalize(
            request.TenantId,
            out string? tenantId) &&
        scopeContext.IsEnabled &&
        string.Equals(
            tenantId,
            scopeContext.ScopeId,
            StringComparison.Ordinal) &&
        string.Equals(
            request.Coordinate.OwnerKey,
            this.OwnerKey,
            StringComparison.Ordinal) &&
        string.Equals(
            request.Coordinate.RecordType,
            this.RecordType,
            StringComparison.Ordinal) &&
        request.Coordinate.RecordId != Guid.Empty &&
        request.Coordinate.RecordVersion > 0;

    private static CountryPolicyBinding ToPolicyBinding(
        StaffEmploymentGovernance governance) =>
        new(
            governance.Binding.OperatingCountryCode,
            governance.Binding.PolicyId,
            governance.Binding.PolicyVersion,
            governance.Binding.DataRegionId,
            governance.Binding.TransferProfileId,
            governance.Binding.RetentionPolicyId,
            governance.Binding.RetentionPolicyVersion,
            governance.Binding.ContentSha256,
            governance.AcceptedAcknowledgements
                .Select(acknowledgement =>
                    new CountryPolicyAcknowledgement(
                        acknowledgement.AcknowledgementId,
                        acknowledgement.AcknowledgementVersion))
                .ToArray());

    private static bool MatchesPolicy(
        CountryPolicyEvidence operation,
        CountryPolicyEvidence retention) =>
        string.Equals(
            operation.OperatingCountryCode,
            retention.OperatingCountryCode,
            StringComparison.Ordinal) &&
        string.Equals(
            operation.PolicyId,
            retention.PolicyId,
            StringComparison.Ordinal) &&
        operation.PolicyVersion == retention.PolicyVersion &&
        string.Equals(
            operation.RetentionPolicyId,
            retention.RetentionPolicyId,
            StringComparison.Ordinal) &&
        operation.RetentionPolicyVersion ==
            retention.RetentionPolicyVersion &&
        string.Equals(
            operation.ContentSha256,
            retention.ContentSha256,
            StringComparison.Ordinal);

    private static DataRightsAnonymisationPolicyContributionResult Denied(
        string code) =>
        DataRightsAnonymisationPolicyContributionResult.Denied(code);
}
