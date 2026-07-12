namespace BunkFy.Modules.Guests.Application;

using Gma.Framework.Results;
using BunkFy.Modules.Guests.Domain.Errors;

public static class GuestsApplicationErrors
{
    public static readonly Error GuestNotFound = new("Guests.GuestNotFound", "The guest profile was not found.");
    public static readonly Error TenantRequired = new("Guests.TenantRequired", "A tenant context is required.");
    public static readonly Error PropertyUnavailable = new("Guests.PropertyUnavailable", "The property is unavailable for guest records.");
    public static Error VersionConflict => GuestsDomainErrors.VersionConflict;
    public static Error GuestArchived => GuestsDomainErrors.GuestArchived;
    public static Error GuestAlreadyArchived => GuestsDomainErrors.GuestAlreadyArchived;
}
