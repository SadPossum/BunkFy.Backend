namespace BunkFy.Modules.Reservations.Application.Handlers;

using Gma.Framework.Application.Events;
using Gma.Framework.Domain;

internal sealed class ReservationInboxDomainEventDispatcher(IDomainEventDispatcher dispatcher)
{
    public async Task DispatchAsync(IAggregateRoot aggregate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(aggregate);

        if (aggregate.DomainEvents.Count == 0)
        {
            return;
        }

        await dispatcher
            .DispatchAsync(aggregate.DomainEvents.ToArray(), cancellationToken)
            .ConfigureAwait(false);
        aggregate.ClearDomainEvents();
    }
}
