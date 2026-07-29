namespace BunkFy.Modules.Staff.Application.Handlers;

using BunkFy.DataGovernance;
using BunkFy.Modules.Staff.Application.Commands;
using BunkFy.Modules.Staff.Application.Mapping;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Domain.Governance;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using DomainAcknowledgement =
    BunkFy.Modules.Staff.Domain.Governance.StaffEmploymentGovernanceAcknowledgement;

internal sealed class ConfigureStaffEmploymentGovernanceCommandHandler(
    IStaffMemberRepository members,
    IStaffEmploymentGovernanceRepository governanceRepository,
    IStaffOperationLock operationLock,
    CountryPolicyRegistry countryPolicies,
    IScopeContext scopeContext,
    ISystemClock clock,
    IIdGenerator ids)
    : ICommandHandler<
        ConfigureStaffEmploymentGovernanceCommand,
        StaffEmploymentGovernanceChangeReceiptDto>
{
    internal const string AccommodationType = "hostel";
    internal const string PurposeCode = "staff-employment-governance";
    internal const string OperatorProvenance =
        "authorized-workspace-operator";

    public async Task<Result<StaffEmploymentGovernanceChangeReceiptDto>>
        HandleAsync(
            ConfigureStaffEmploymentGovernanceCommand command,
            CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled ||
            string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<
                StaffEmploymentGovernanceChangeReceiptDto>(
                StaffApplicationErrors.TenantRequired);
        }

        ConfigureStaffEmploymentGovernanceCommand normalized =
            Normalize(command);
        string? actorId = NormalizeActor(normalized.ActorId);
        if (!IsValidRequest(normalized) || actorId is null)
        {
            return Result.Failure<
                StaffEmploymentGovernanceChangeReceiptDto>(
                StaffApplicationErrors
                    .EmploymentGovernanceRequestInvalid);
        }

        string requestSha256 =
            StaffEmploymentGovernanceFingerprint.Compute(
                normalized,
                actorId);
        StaffEmploymentGovernanceChangeReceipt? existing =
            await governanceRepository
                .FindReceiptByIdempotencyKeyAsync(
                    normalized.IdempotencyKey,
                    cancellationToken)
                .ConfigureAwait(false);
        if (existing is not null)
        {
            return existing.MatchesReplay(
                normalized.StaffMemberId,
                normalized.ExpectedStaffVersion,
                normalized.ExpectedGovernanceVersion,
                requestSha256)
                ? Result.Success(existing.ToDto())
                : Result.Failure<
                    StaffEmploymentGovernanceChangeReceiptDto>(
                    StaffApplicationErrors
                        .EmploymentGovernanceIdempotencyConflict);
        }

        if (!await operationLock.TryAcquireStaffMemberAsync(
                scopeContext.ScopeId,
                normalized.StaffMemberId,
                cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<
                StaffEmploymentGovernanceChangeReceiptDto>(
                StaffApplicationErrors.StaffMemberNotFound);
        }

        existing = await governanceRepository
            .FindReceiptByIdempotencyKeyAsync(
                normalized.IdempotencyKey,
                cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return existing.MatchesReplay(
                normalized.StaffMemberId,
                normalized.ExpectedStaffVersion,
                normalized.ExpectedGovernanceVersion,
                requestSha256)
                ? Result.Success(existing.ToDto())
                : Result.Failure<
                    StaffEmploymentGovernanceChangeReceiptDto>(
                    StaffApplicationErrors
                        .EmploymentGovernanceIdempotencyConflict);
        }

        StaffMember? member = await members.GetForDataRightsAsync(
            normalized.StaffMemberId,
            cancellationToken).ConfigureAwait(false);
        if (member is null)
        {
            return Result.Failure<
                StaffEmploymentGovernanceChangeReceiptDto>(
                StaffApplicationErrors.StaffMemberNotFound);
        }

        if (member.Version != normalized.ExpectedStaffVersion)
        {
            return Result.Failure<
                StaffEmploymentGovernanceChangeReceiptDto>(
                StaffApplicationErrors
                    .EmploymentGovernanceStaffVersionConflict);
        }

        StaffEmploymentGovernance? governance =
            await governanceRepository.GetAsync(
                normalized.StaffMemberId,
                cancellationToken).ConfigureAwait(false);
        long currentGovernanceVersion = governance?.Version ?? 0;
        if (currentGovernanceVersion !=
            normalized.ExpectedGovernanceVersion)
        {
            return Result.Failure<
                StaffEmploymentGovernanceChangeReceiptDto>(
                StaffApplicationErrors
                    .EmploymentGovernanceVersionConflict);
        }

        DateTimeOffset nowUtc =
            ToPersistencePrecision(clock.UtcNow);
        CountryPolicyAcknowledgement[] requestedAcknowledgements =
            normalized.AcceptedAcknowledgements
                .Select(item => new CountryPolicyAcknowledgement(
                    item.AcknowledgementId,
                    item.AcknowledgementVersion))
                .ToArray();
        CountryPolicyDecision decision =
            countryPolicies.EvaluateBinding(
                new CountryPolicyBindingRequest(
                    normalized.OperatingCountryCode,
                    normalized.PolicyId,
                    normalized.PolicyVersion,
                    normalized.DataRegionId,
                    normalized.TransferProfileId,
                    normalized.RetentionPolicyId,
                    normalized.RetentionPolicyVersion,
                    requestedAcknowledgements,
                    AccommodationType,
                    PurposeCode,
                    CountryPolicySurface.ApiWrite,
                    OperatorProvenance,
                    nowUtc));
        if (!decision.IsAllowed || decision.Evidence is null)
        {
            return Result.Failure<
                StaffEmploymentGovernanceChangeReceiptDto>(
                decision.Reason ==
                    CountryPolicyDecisionReason.InvalidRequest
                    ? StaffApplicationErrors
                        .EmploymentGovernanceRequestInvalid
                    : StaffApplicationErrors
                        .EmploymentGovernancePolicyDenied(
                            decision.Reason));
        }

        Result<StaffEmploymentGovernanceBinding> binding =
            CreateBinding(decision.Evidence);
        if (binding.IsFailure)
        {
            return Result.Failure<
                StaffEmploymentGovernanceChangeReceiptDto>(
                binding.Error);
        }

        Result<IReadOnlyCollection<DomainAcknowledgement>>
            acknowledgements = CreateAcknowledgements(
                decision.Evidence.AcceptedAcknowledgements);
        if (acknowledgements.IsFailure)
        {
            return Result.Failure<
                StaffEmploymentGovernanceChangeReceiptDto>(
                acknowledgements.Error);
        }

        if (governance is null)
        {
            Result<StaffEmploymentGovernance> configured =
                StaffEmploymentGovernance.Configure(
                    scopeContext.ScopeId,
                    normalized.StaffMemberId,
                    normalized.ExpectedStaffVersion,
                    binding.Value,
                    acknowledgements.Value,
                    actorId,
                    nowUtc);
            if (configured.IsFailure)
            {
                return Result.Failure<
                    StaffEmploymentGovernanceChangeReceiptDto>(
                    configured.Error);
            }

            governance = configured.Value;
            await governanceRepository.AddAsync(
                governance,
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            Result replaced = governance.Replace(
                normalized.ExpectedGovernanceVersion,
                normalized.ExpectedStaffVersion,
                binding.Value,
                acknowledgements.Value,
                actorId,
                nowUtc);
            if (replaced.IsFailure)
            {
                return Result.Failure<
                    StaffEmploymentGovernanceChangeReceiptDto>(
                    replaced.Error);
            }
        }

        Result<StaffEmploymentGovernanceChangeReceipt> receipt =
            StaffEmploymentGovernanceChangeReceipt.Create(
                ids.NewId(),
                scopeContext.ScopeId,
                normalized.IdempotencyKey,
                governance,
                currentGovernanceVersion,
                requestSha256,
                actorId,
                nowUtc);
        if (receipt.IsFailure)
        {
            return Result.Failure<
                StaffEmploymentGovernanceChangeReceiptDto>(
                receipt.Error);
        }

        await governanceRepository.AddReceiptAsync(
            receipt.Value,
            cancellationToken).ConfigureAwait(false);
        return Result.Success(receipt.Value.ToDto());
    }

    private static ConfigureStaffEmploymentGovernanceCommand Normalize(
        ConfigureStaffEmploymentGovernanceCommand command) =>
        command with
        {
            OperatingCountryCode =
                command.OperatingCountryCode?.Trim().ToUpperInvariant() ??
                string.Empty,
            PolicyId =
                command.PolicyId?.Trim().ToLowerInvariant() ??
                string.Empty,
            DataRegionId =
                command.DataRegionId?.Trim().ToLowerInvariant() ??
                string.Empty,
            TransferProfileId =
                command.TransferProfileId?.Trim().ToLowerInvariant() ??
                string.Empty,
            RetentionPolicyId =
                command.RetentionPolicyId?.Trim().ToLowerInvariant() ??
                string.Empty,
            AcceptedAcknowledgements =
                command.AcceptedAcknowledgements?
                    .Select(item =>
                        new StaffEmploymentGovernanceAcknowledgementDto(
                            item.AcknowledgementId?
                                .Trim()
                                .ToLowerInvariant() ?? string.Empty,
                            item.AcknowledgementVersion))
                    .OrderBy(
                        item => item.AcknowledgementId,
                        StringComparer.Ordinal)
                    .ThenBy(item => item.AcknowledgementVersion)
                    .ToArray() ?? [],
            ActorId = command.ActorId?.Trim() ?? string.Empty
        };

    private static bool IsValidRequest(
        ConfigureStaffEmploymentGovernanceCommand command) =>
        command.IdempotencyKey != Guid.Empty &&
        command.StaffMemberId != Guid.Empty &&
        command.ExpectedStaffVersion >= 1 &&
        command.ExpectedGovernanceVersion >= 0 &&
        command.PolicyVersion >= 1 &&
        command.RetentionPolicyVersion >= 1 &&
        command.AcceptedAcknowledgements.Count <=
            CountryPolicyRegistry.MaximumAcceptedAcknowledgements;

    private static string? NormalizeActor(string actorId) =>
        actorId is { Length: > 0 } &&
        actorId.Length <= StaffMember.ActorIdMaxLength &&
        !actorId.Any(char.IsControl)
            ? actorId
            : null;

    private static Result<StaffEmploymentGovernanceBinding>
        CreateBinding(CountryPolicyEvidence evidence) =>
        StaffEmploymentGovernanceBinding.Create(
            evidence.OperatingCountryCode,
            evidence.PolicyId,
            evidence.PolicyVersion,
            evidence.DataRegionId,
            evidence.TransferProfileId,
            evidence.RetentionPolicyId,
            evidence.RetentionPolicyVersion,
            evidence.ContentSha256,
            evidence.EffectiveAtUtc,
            evidence.ExpiresAtUtc,
            evidence.EvaluatedAtUtc);

    private static Result<IReadOnlyCollection<DomainAcknowledgement>>
        CreateAcknowledgements(
            IReadOnlyCollection<CountryPolicyAcknowledgement>
                acknowledgements)
    {
        List<DomainAcknowledgement> values = [];
        foreach (CountryPolicyAcknowledgement acknowledgement in
                 acknowledgements)
        {
            Result<DomainAcknowledgement> result =
                DomainAcknowledgement.Create(
                    acknowledgement.AcknowledgementId,
                    acknowledgement.AcknowledgementVersion);
            if (result.IsFailure)
            {
                return Result.Failure<
                    IReadOnlyCollection<DomainAcknowledgement>>(
                    result.Error);
            }

            values.Add(result.Value);
        }

        return Result.Success<
            IReadOnlyCollection<DomainAcknowledgement>>(values);
    }

    private static DateTimeOffset ToPersistencePrecision(
        DateTimeOffset value)
    {
        const long ticksPerMicrosecond =
            TimeSpan.TicksPerMillisecond / 1000;
        return new(
            value.Ticks - (value.Ticks % ticksPerMicrosecond),
            value.Offset);
    }
}
