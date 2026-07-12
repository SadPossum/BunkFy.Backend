namespace BunkFy.Modules.Guests.Application.Queries;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Guests.Contracts;

public sealed record GetGuestProfileQuery(Guid PropertyId, Guid GuestId) : IQuery<GuestProfileDto>;
