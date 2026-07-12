namespace BunkFy.Modules.Properties.Application.Ports;

using BunkFy.Modules.Properties.Domain.Aggregates;

public interface IPropertyRepository
{
    Task AddAsync(Property property, CancellationToken cancellationToken);
    Task<Property?> GetAsync(Guid propertyId, CancellationToken cancellationToken);
    Task<bool> CodeExistsAsync(string code, Guid? excludingPropertyId, CancellationToken cancellationToken);
}
