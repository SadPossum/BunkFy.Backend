namespace BunkFy.Modules.Guests.Application.Ports;

using BunkFy.Modules.Properties.Contracts;

public interface IGuestPropertyProjectionRepository
{
    Task<bool> IsActiveAsync(Guid propertyId, CancellationToken cancellationToken);
    Task ApplyAsync(GuestPropertyProjectionWriteModel property, CancellationToken cancellationToken);
}

public sealed record GuestPropertyProjectionWriteModel(
    string ScopeId,
    Guid PropertyId,
    string Name,
    PropertyStatus Status,
    long Version);
