namespace BunkFy.Modules.Guests.Application.Queries;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Guests.Contracts;

public sealed record GetGuestStayHistoryQuery(Guid PropertyId, Guid GuestId)
    : IQuery<IReadOnlyCollection<GuestStayHistoryItem>>;
