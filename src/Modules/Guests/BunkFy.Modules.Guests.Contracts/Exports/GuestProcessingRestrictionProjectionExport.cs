namespace BunkFy.Modules.Guests.Contracts;

public sealed record GuestProcessingRestrictionProjectionExport(
    string TenantId,
    Guid PropertyId,
    Guid GuestId,
    int ContractVersion,
    long ProjectionRevision,
    bool IsRestricted);
