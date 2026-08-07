namespace BunkFy.Modules.Properties.Tests;

using BunkFy.Modules.Properties.Application.Ports;

internal sealed class RecordingPropertyMutationOperationRepository(
    params PropertyMutationOperationRecord[] existing)
    : IPropertyMutationOperationRepository
{
    private readonly Dictionary<(Guid PropertyId, Guid OperationId), PropertyMutationOperationRecord>
        operations = existing.ToDictionary(
            operation => (operation.PropertyId, operation.OperationId));

    public List<PropertyMutationOperationRecord> Added { get; } = [];

    public int ReadCount { get; private set; }

    public Task<PropertyMutationOperationRecord?> GetAsync(
        Guid propertyId,
        Guid operationId,
        CancellationToken cancellationToken)
    {
        this.ReadCount++;
        this.operations.TryGetValue(
            (propertyId, operationId),
            out PropertyMutationOperationRecord? operation);
        return Task.FromResult(operation);
    }

    public Task AddAsync(
        PropertyMutationOperationRecord operation,
        CancellationToken cancellationToken)
    {
        this.operations.Add(
            (operation.PropertyId, operation.OperationId),
            operation);
        this.Added.Add(operation);
        return Task.CompletedTask;
    }
}
