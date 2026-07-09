namespace Properties.Application;

using Properties.Domain.Errors;
using Gma.Framework.Results;

public static class PropertiesApplicationErrors
{
    public static readonly Error PropertyNotFound = PropertiesDomainErrors.PropertyNotFound;
    public static readonly Error PropertyCodeAlreadyExists = PropertiesDomainErrors.PropertyCodeAlreadyExists;
    public static readonly Error PropertyStatusUnknown = PropertiesDomainErrors.PropertyStatusUnknown;
    public static readonly Error RoomAlreadyExists = PropertiesDomainErrors.RoomAlreadyExists;
    public static readonly Error RoomNotFound = PropertiesDomainErrors.RoomNotFound;
    public static readonly Error RoomStatusUnknown = PropertiesDomainErrors.RoomStatusUnknown;
    public static readonly Error RoomRetired = PropertiesDomainErrors.RoomRetired;
    public static readonly Error BedAlreadyExists = PropertiesDomainErrors.BedAlreadyExists;
    public static readonly Error BedNotFound = PropertiesDomainErrors.BedNotFound;
    public static readonly Error BedStatusUnknown = PropertiesDomainErrors.BedStatusUnknown;
    public static readonly Error BedAlreadyRetired = PropertiesDomainErrors.BedAlreadyRetired;
}
