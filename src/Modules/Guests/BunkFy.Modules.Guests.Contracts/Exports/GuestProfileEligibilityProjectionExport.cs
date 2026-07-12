namespace BunkFy.Modules.Guests.Contracts;

public sealed record GuestProfileEligibilityProjectionExport(
    string TenantId,
    Guid GuestId,
    Guid OriginPropertyId,
    GuestStatus Status,
    long GuestVersion);
