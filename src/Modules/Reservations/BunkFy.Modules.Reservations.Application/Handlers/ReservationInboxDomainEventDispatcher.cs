namespace BunkFy.Modules.Reservations.Application.Handlers;

using Gma.Framework.Application.Events;
using Gma.Framework.Domain;

internal sealed class ReservationInboxDomainEventDispatcher(IDomainEventDispatcher dispatcher)
{
    public Task DispatchAsync(IAggregateRoot aggregate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(aggregate);

        return aggregate.DomainEvents.Count == 0
            ? Task.CompletedTask
            : dispatcher.DispatchAsync(aggregate.DomainEvents.ToArray(), cancellationToken);
    }
}
