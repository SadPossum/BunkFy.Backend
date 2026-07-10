namespace Reservations.Contracts;

using Gma.Framework.Messaging;

public static class ReservationsIntegrationSubjects
{
    public static string CreateReservationCreated(string prefix = IntegrationEventNaming.DefaultSubjectPrefix) =>
        IntegrationEventNaming.CreateSubject(prefix, ReservationsModuleMetadata.Name, ReservationCreatedIntegrationEvent.EventType, ReservationCreatedIntegrationEvent.EventVersion);

    public static string CreateReservationConfirmed(string prefix = IntegrationEventNaming.DefaultSubjectPrefix) =>
        IntegrationEventNaming.CreateSubject(prefix, ReservationsModuleMetadata.Name, ReservationConfirmedIntegrationEvent.EventType, ReservationConfirmedIntegrationEvent.EventVersion);

    public static string CreateReservationAllocationRejected(string prefix = IntegrationEventNaming.DefaultSubjectPrefix) =>
        IntegrationEventNaming.CreateSubject(prefix, ReservationsModuleMetadata.Name, ReservationAllocationRejectedIntegrationEvent.EventType, ReservationAllocationRejectedIntegrationEvent.EventVersion);

    public static string CreateReservationCancelled(string prefix = IntegrationEventNaming.DefaultSubjectPrefix) =>
        IntegrationEventNaming.CreateSubject(prefix, ReservationsModuleMetadata.Name, ReservationCancelledIntegrationEvent.EventType, ReservationCancelledIntegrationEvent.EventVersion);
}
