namespace Properties.Application.Ports;

using Properties.Domain.Aggregates;

public interface IPropertyRepository
{
    Task AddAsync(Property property, CancellationToken cancellationToken);
    Task<Property?> GetAsync(Guid propertyId, CancellationToken cancellationToken);
    Task<bool> CodeExistsAsync(string code, Guid? excludingPropertyId, CancellationToken cancellationToken);
}
