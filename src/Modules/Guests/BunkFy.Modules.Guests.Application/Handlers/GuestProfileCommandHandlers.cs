namespace BunkFy.Modules.Guests.Application.Handlers;

using BunkFy.DataGovernance;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using BunkFy.Modules.Guests.Application.Commands;
using BunkFy.Modules.Guests.Application.Mapping;
using BunkFy.Modules.Guests.Application.Ports;
using BunkFy.Modules.Guests.Application.Policies;
using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Domain.Aggregates;

internal sealed class CreateGuestProfileCommandHandler(
    IGuestProfileRepository profiles,
    GuestMutationCoordinator mutations,
    IGuestCountryPolicyAdmission countryPolicy,
    IScopeContext scopeContext,
    ISystemClock clock,
    IIdGenerator ids) : ICommandHandler<CreateGuestProfileCommand, GuestMutationReceiptDto>
{
    public async Task<Result<GuestMutationReceiptDto>> HandleAsync(
        CreateGuestProfileCommand command,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled || string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<GuestMutationReceiptDto>(GuestsApplicationErrors.TenantRequired);
        }

        CountryPolicyDecision policyDecision = await countryPolicy.EvaluateAsync(
            command.PropertyId,
            GuestCountryPolicyAdmission.GuestProfileManagementPurpose,
            CountryPolicySurface.ApiWrite,
            GuestCountryPolicyAdmission.AuthorizedOperatorProvenance,
            cancellationToken).ConfigureAwait(false);
        if (!policyDecision.IsAllowed)
        {
            return Result.Failure<GuestMutationReceiptDto>(
                GuestsApplicationErrors.CountryPolicyDenied(policyDecision.Reason));
        }

        DateTimeOffset nowUtc = clock.UtcNow;
        Result<GuestProfileCreationSnapshot> creation = GuestProfileCreationSnapshot.Capture(
            command.PropertyId,
            command.DisplayName,
            command.LegalName,
            command.Email,
            command.Phone,
            command.DateOfBirth,
            command.NationalityCountryCode,
            command.PreferredLanguageTag,
            command.Notes,
            command.ActorId,
            nowUtc,
            command.CreationConfirmationId);
        if (creation.IsFailure)
        {
            return Result.Failure<GuestMutationReceiptDto>(creation.Error);
        }

        GuestProfile? existing = await mutations.AcquireCreationAsync(
            command.OperationId,
            cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing.MatchesCreation(creation.Value)
                ? Result.Success(existing.ToMutationReceipt())
                : Result.Failure<GuestMutationReceiptDto>(
                    GuestsApplicationErrors.CreationOperationConflict);
        }

        Result<GuestProfile> created = GuestProfile.Create(
            command.OperationId,
            scopeContext.ScopeId,
            command.PropertyId,
            command.DisplayName,
            command.LegalName,
            command.Email,
            command.Phone,
            command.DateOfBirth,
            command.NationalityCountryCode,
            command.PreferredLanguageTag,
            command.Notes,
            command.ActorId,
            ids.NewId(),
            nowUtc,
            command.CreationConfirmationId);
        if (created.IsFailure)
        {
            return Result.Failure<GuestMutationReceiptDto>(created.Error);
        }

        await profiles.AddUnderAcquiredOperationLockAsync(
            created.Value,
            cancellationToken).ConfigureAwait(false);
        return Result.Success(created.Value.ToMutationReceipt());
    }
}

internal sealed class UpdateGuestProfileCommandHandler(
    IGuestProfileRepository profiles,
    IGuestOperationLock operationLock,
    IGuestCountryPolicyAdmission countryPolicy,
    ISystemClock clock,
    IIdGenerator ids) : ICommandHandler<UpdateGuestProfileCommand, GuestMutationReceiptDto>
{
    public async Task<Result<GuestMutationReceiptDto>> HandleAsync(
        UpdateGuestProfileCommand command,
        CancellationToken cancellationToken)
    {
        CountryPolicyDecision policyDecision = await countryPolicy.EvaluateAsync(
            command.PropertyId,
            GuestCountryPolicyAdmission.GuestProfileManagementPurpose,
            CountryPolicySurface.ApiWrite,
            GuestCountryPolicyAdmission.AuthorizedOperatorProvenance,
            cancellationToken).ConfigureAwait(false);
        if (!policyDecision.IsAllowed)
        {
            return Result.Failure<GuestMutationReceiptDto>(
                GuestsApplicationErrors.CountryPolicyDenied(policyDecision.Reason));
        }

        GuestProfile? profile = await profiles.GetVisibleAsync(
            command.PropertyId, command.GuestId, cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            return Result.Failure<GuestMutationReceiptDto>(GuestsApplicationErrors.GuestNotFound);
        }

        await operationLock.AcquireGuestAsync(
            profile.ScopeId,
            command.GuestId,
            cancellationToken).ConfigureAwait(false);
        profile = await profiles.GetVisibleAsync(
            command.PropertyId, command.GuestId, cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            return Result.Failure<GuestMutationReceiptDto>(GuestsApplicationErrors.GuestNotFound);
        }

        Result updated = profile.Update(
            command.DisplayName,
            command.LegalName,
            command.Email,
            command.Phone,
            command.DateOfBirth,
            command.NationalityCountryCode,
            command.PreferredLanguageTag,
            command.Notes,
            command.ExpectedVersion,
            command.ActorId,
            ids.NewId(),
            clock.UtcNow);
        return updated.IsSuccess
            ? Result.Success(profile.ToMutationReceipt())
            : Result.Failure<GuestMutationReceiptDto>(updated.Error);
    }
}

internal sealed class ArchiveGuestProfileCommandHandler(
    IGuestProfileRepository profiles,
    IGuestOperationLock operationLock,
    ISystemClock clock,
    IIdGenerator ids) : ICommandHandler<ArchiveGuestProfileCommand, GuestMutationReceiptDto>
{
    public async Task<Result<GuestMutationReceiptDto>> HandleAsync(
        ArchiveGuestProfileCommand command,
        CancellationToken cancellationToken)
    {
        GuestProfile? profile = await profiles.GetVisibleAsync(
            command.PropertyId, command.GuestId, cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            return Result.Failure<GuestMutationReceiptDto>(GuestsApplicationErrors.GuestNotFound);
        }

        await operationLock.AcquireGuestAsync(
            profile.ScopeId,
            command.GuestId,
            cancellationToken).ConfigureAwait(false);
        profile = await profiles.GetVisibleAsync(
            command.PropertyId, command.GuestId, cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            return Result.Failure<GuestMutationReceiptDto>(GuestsApplicationErrors.GuestNotFound);
        }

        Result archived = profile.Archive(
            command.ExpectedVersion,
            command.ActorId,
            ids.NewId(),
            clock.UtcNow);
        return archived.IsSuccess
            ? Result.Success(profile.ToMutationReceipt())
            : Result.Failure<GuestMutationReceiptDto>(archived.Error);
    }
}
