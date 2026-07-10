namespace Properties.Application;

using Properties.Domain.Errors;
using Gma.Framework.Results;

public static class PropertiesApplicationErrors
{
    public static readonly Error AccessDenied = new("Properties.AccessDenied", "The subject cannot access the requested property scope.");
    public static readonly Error TenantRequired = PropertiesDomainErrors.TenantRequired;
    public static readonly Error PropertyNotFound = PropertiesDomainErrors.PropertyNotFound;
    public static readonly Error PropertyCodeAlreadyExists = PropertiesDomainErrors.PropertyCodeAlreadyExists;
    public static readonly Error PropertyStatusUnknown = PropertiesDomainErrors.PropertyStatusUnknown;
    public static readonly Error PropertyAlreadyRetired = PropertiesDomainErrors.PropertyAlreadyRetired;
    public static readonly Error PropertyRetired = PropertiesDomainErrors.PropertyRetired;
    public static readonly Error PropertyHasActiveRooms = PropertiesDomainErrors.PropertyHasActiveRooms;
    public static readonly Error VersionConflict = PropertiesDomainErrors.VersionConflict;
    public static readonly Error RoomAlreadyExists = PropertiesDomainErrors.RoomAlreadyExists;
    public static readonly Error RoomNotFound = PropertiesDomainErrors.RoomNotFound;
    public static readonly Error RoomStatusUnknown = PropertiesDomainErrors.RoomStatusUnknown;
    public static readonly Error RoomRetired = PropertiesDomainErrors.RoomRetired;
    public static readonly Error RoomHasActiveBeds = PropertiesDomainErrors.RoomHasActiveBeds;
    public static readonly Error BedAlreadyExists = PropertiesDomainErrors.BedAlreadyExists;
    public static readonly Error BedNotFound = PropertiesDomainErrors.BedNotFound;
    public static readonly Error BedStatusUnknown = PropertiesDomainErrors.BedStatusUnknown;
    public static readonly Error BedAlreadyRetired = PropertiesDomainErrors.BedAlreadyRetired;
}
