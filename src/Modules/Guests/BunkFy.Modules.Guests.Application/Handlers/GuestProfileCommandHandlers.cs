namespace BunkFy.Modules.Guests.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using BunkFy.Modules.Guests.Application.Commands;
using BunkFy.Modules.Guests.Application.Mapping;
using BunkFy.Modules.Guests.Application.Ports;
using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Domain.Aggregates;

internal sealed class CreateGuestProfileCommandHandler(
    IGuestProfileRepository profiles,
    IGuestPropertyProjectionRepository properties,
    IScopeContext scopeContext,
    ISystemClock clock,
    IIdGenerator ids) : ICommandHandler<CreateGuestProfileCommand, GuestProfileDto>
{
    public async Task<Result<GuestProfileDto>> HandleAsync(
        CreateGuestProfileCommand command,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled || string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<GuestProfileDto>(GuestsApplicationErrors.TenantRequired);
        }

        if (!await properties.IsActiveAsync(command.PropertyId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<GuestProfileDto>(GuestsApplicationErrors.PropertyUnavailable);
        }

        Result<GuestProfile> created = GuestProfile.Create(
            ids.NewId(),
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
            clock.UtcNow);
        if (created.IsFailure)
        {
            return Result.Failure<GuestProfileDto>(created.Error);
        }

        await profiles.AddAsync(created.Value, cancellationToken).ConfigureAwait(false);
        return Result.Success(created.Value.ToDto());
    }
}

internal sealed class UpdateGuestProfileCommandHandler(
    IGuestProfileRepository profiles,
    ISystemClock clock,
    IIdGenerator ids) : ICommandHandler<UpdateGuestProfileCommand, GuestProfileDto>
{
    public async Task<Result<GuestProfileDto>> HandleAsync(
        UpdateGuestProfileCommand command,
        CancellationToken cancellationToken)
    {
        GuestProfile? profile = await profiles.GetVisibleAsync(
            command.PropertyId, command.GuestId, cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            return Result.Failure<GuestProfileDto>(GuestsApplicationErrors.GuestNotFound);
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
            ? Result.Success(profile.ToDto())
            : Result.Failure<GuestProfileDto>(updated.Error);
    }
}

internal sealed class ArchiveGuestProfileCommandHandler(
    IGuestProfileRepository profiles,
    ISystemClock clock,
    IIdGenerator ids) : ICommandHandler<ArchiveGuestProfileCommand, GuestProfileDto>
{
    public async Task<Result<GuestProfileDto>> HandleAsync(
        ArchiveGuestProfileCommand command,
        CancellationToken cancellationToken)
    {
        GuestProfile? profile = await profiles.GetVisibleAsync(
            command.PropertyId, command.GuestId, cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            return Result.Failure<GuestProfileDto>(GuestsApplicationErrors.GuestNotFound);
        }

        Result archived = profile.Archive(
            command.ExpectedVersion,
            command.ActorId,
            ids.NewId(),
            clock.UtcNow);
        return archived.IsSuccess
            ? Result.Success(profile.ToDto())
            : Result.Failure<GuestProfileDto>(archived.Error);
    }
}
