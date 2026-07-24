namespace BunkFy.Modules.Guests.Contracts;

using Gma.Framework.Messaging;

public static class GuestsIntegrationSubjects
{
    public static string CreateProfileCreated(string prefix = IntegrationEventNaming.DefaultSubjectPrefix) =>
        IntegrationEventNaming.CreateSubject(
            prefix,
            GuestsModuleMetadata.Name,
            GuestProfileCreatedIntegrationEvent.EventType,
            GuestProfileCreatedIntegrationEvent.EventVersion);

    public static string CreateProfileUpdated(string prefix = IntegrationEventNaming.DefaultSubjectPrefix) =>
        IntegrationEventNaming.CreateSubject(
            prefix,
            GuestsModuleMetadata.Name,
            GuestProfileUpdatedIntegrationEvent.EventType,
            GuestProfileUpdatedIntegrationEvent.EventVersion);

    public static string CreateProfileArchived(string prefix = IntegrationEventNaming.DefaultSubjectPrefix) =>
        IntegrationEventNaming.CreateSubject(
            prefix,
            GuestsModuleMetadata.Name,
            GuestProfileArchivedIntegrationEvent.EventType,
            GuestProfileArchivedIntegrationEvent.EventVersion);

    public static string CreateProcessingRestrictionChanged(
        string prefix = IntegrationEventNaming.DefaultSubjectPrefix) =>
        IntegrationEventNaming.CreateSubject(
            prefix,
            GuestsModuleMetadata.Name,
            GuestProcessingRestrictionChangedIntegrationEvent.EventType,
            GuestProcessingRestrictionChangedIntegrationEvent.EventVersion);

    public static string CreateReservationGuestLinked(string prefix = IntegrationEventNaming.DefaultSubjectPrefix) =>
        IntegrationEventNaming.CreateSubject(
            prefix,
            GuestsModuleMetadata.ReservationsProducerModuleName,
            ReservationGuestLinkedIntegrationEvent.EventType,
            ReservationGuestLinkedIntegrationEvent.EventVersion);

    public static string CreateReservationGuestStayChanged(string prefix = IntegrationEventNaming.DefaultSubjectPrefix) =>
        IntegrationEventNaming.CreateSubject(
            prefix,
            GuestsModuleMetadata.ReservationsProducerModuleName,
            ReservationGuestStayChangedIntegrationEvent.EventType,
            ReservationGuestStayChangedIntegrationEvent.EventVersion);
}
