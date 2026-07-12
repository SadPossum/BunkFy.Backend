namespace BunkFy.Modules.Guests.Application.Commands;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Guests.Contracts;

public sealed record CreateGuestProfileCommand(
    Guid PropertyId,
    string DisplayName,
    string? LegalName,
    string? Email,
    string? Phone,
    DateOnly? DateOfBirth,
    string? NationalityCountryCode,
    string? PreferredLanguageTag,
    string? Notes,
    string ActorId) : ITransactionalCommand<GuestProfileDto>;
