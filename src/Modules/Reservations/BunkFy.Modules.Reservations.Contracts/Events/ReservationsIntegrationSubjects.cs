namespace BunkFy.Modules.Reservations.Contracts;

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

    public static string CreateReservationCheckedIn(string prefix = IntegrationEventNaming.DefaultSubjectPrefix) =>
        IntegrationEventNaming.CreateSubject(prefix, ReservationsModuleMetadata.Name, ReservationCheckedInIntegrationEvent.EventType, ReservationCheckedInIntegrationEvent.EventVersion);

    public static string CreateReservationNoShow(string prefix = IntegrationEventNaming.DefaultSubjectPrefix) =>
        IntegrationEventNaming.CreateSubject(prefix, ReservationsModuleMetadata.Name, ReservationNoShowIntegrationEvent.EventType, ReservationNoShowIntegrationEvent.EventVersion);

    public static string CreateReservationCheckedOut(string prefix = IntegrationEventNaming.DefaultSubjectPrefix) =>
        IntegrationEventNaming.CreateSubject(prefix, ReservationsModuleMetadata.Name, ReservationCheckedOutIntegrationEvent.EventType, ReservationCheckedOutIntegrationEvent.EventVersion);

    public static string CreateExternalOperationCompleted(string prefix = IntegrationEventNaming.DefaultSubjectPrefix) =>
        IntegrationEventNaming.CreateSubject(prefix, ReservationsModuleMetadata.Name, ExternalReservationOperationCompletedIntegrationEvent.EventType, ExternalReservationOperationCompletedIntegrationEvent.EventVersion);
}
