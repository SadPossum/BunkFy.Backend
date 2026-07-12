namespace BunkFy.Modules.Staff.Application.Ports;

using BunkFy.Modules.Properties.Contracts;

public interface IStaffPropertyProjectionRepository
{
    Task<bool> IsActiveAsync(Guid propertyId, CancellationToken cancellationToken);
    Task ApplyAsync(StaffPropertyProjectionWriteModel property, CancellationToken cancellationToken);
}

public sealed record StaffPropertyProjectionWriteModel(string ScopeId, Guid PropertyId,
    string Name, PropertyStatus Status, long Version);
