namespace BunkFy.Modules.Guests.Application.Ports;

public interface IGuestAnonymisationExecutionBoundary
{
    Task AcquireAsync(
        string tenantId,
        Guid guestId,
        CancellationToken cancellationToken);
}
