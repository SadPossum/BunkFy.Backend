namespace Properties.Contracts;

using Gma.Framework.Messaging;

public static class PropertiesIntegrationSubjects
{
    public static string PropertyCreated => CreatePropertyCreated();
    public static string PropertyUpdated => CreatePropertyUpdated();
    public static string RoomCreated => CreateRoomCreated();
    public static string RoomUpdated => CreateRoomUpdated();
    public static string RoomRetired => CreateRoomRetired();
    public static string BedAdded => CreateBedAdded();
    public static string BedUpdated => CreateBedUpdated();
    public static string BedRetired => CreateBedRetired();

    public static string CreatePropertyCreated(string subjectPrefix = IntegrationEventNaming.DefaultSubjectPrefix) =>
        IntegrationEventNaming.CreateSubject(subjectPrefix, PropertiesModuleMetadata.Name, PropertyCreatedIntegrationEvent.EventType, PropertyCreatedIntegrationEvent.EventVersion);

    public static string CreatePropertyUpdated(string subjectPrefix = IntegrationEventNaming.DefaultSubjectPrefix) =>
        IntegrationEventNaming.CreateSubject(subjectPrefix, PropertiesModuleMetadata.Name, PropertyUpdatedIntegrationEvent.EventType, PropertyUpdatedIntegrationEvent.EventVersion);

    public static string CreateRoomCreated(string subjectPrefix = IntegrationEventNaming.DefaultSubjectPrefix) =>
        IntegrationEventNaming.CreateSubject(subjectPrefix, PropertiesModuleMetadata.Name, RoomCreatedIntegrationEvent.EventType, RoomCreatedIntegrationEvent.EventVersion);

    public static string CreateRoomUpdated(string subjectPrefix = IntegrationEventNaming.DefaultSubjectPrefix) =>
        IntegrationEventNaming.CreateSubject(subjectPrefix, PropertiesModuleMetadata.Name, RoomUpdatedIntegrationEvent.EventType, RoomUpdatedIntegrationEvent.EventVersion);

    public static string CreateRoomRetired(string subjectPrefix = IntegrationEventNaming.DefaultSubjectPrefix) =>
        IntegrationEventNaming.CreateSubject(subjectPrefix, PropertiesModuleMetadata.Name, RoomRetiredIntegrationEvent.EventType, RoomRetiredIntegrationEvent.EventVersion);

    public static string CreateBedAdded(string subjectPrefix = IntegrationEventNaming.DefaultSubjectPrefix) =>
        IntegrationEventNaming.CreateSubject(subjectPrefix, PropertiesModuleMetadata.Name, BedAddedIntegrationEvent.EventType, BedAddedIntegrationEvent.EventVersion);

    public static string CreateBedUpdated(string subjectPrefix = IntegrationEventNaming.DefaultSubjectPrefix) =>
        IntegrationEventNaming.CreateSubject(subjectPrefix, PropertiesModuleMetadata.Name, BedUpdatedIntegrationEvent.EventType, BedUpdatedIntegrationEvent.EventVersion);

    public static string CreateBedRetired(string subjectPrefix = IntegrationEventNaming.DefaultSubjectPrefix) =>
        IntegrationEventNaming.CreateSubject(subjectPrefix, PropertiesModuleMetadata.Name, BedRetiredIntegrationEvent.EventType, BedRetiredIntegrationEvent.EventVersion);
}
