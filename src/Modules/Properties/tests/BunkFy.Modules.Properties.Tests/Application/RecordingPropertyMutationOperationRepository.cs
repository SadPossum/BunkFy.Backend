namespace BunkFy.Modules.Properties.Tests;

using BunkFy.Modules.Properties.Application.Ports;

internal sealed class RecordingPropertyMutationOperationRepository(
    params PropertyMutationOperationRecord[] existing)
    : IPropertyMutationOperationRepository
{
    private readonly Dictionary<
        (PropertyMutationResourceKind ResourceKind, Guid ResourceId, Guid OperationId),
        PropertyMutationOperationRecord>
        operations = existing.ToDictionary(
            operation => (
                operation.ResourceKind,
                operation.ResourceId,
                operation.OperationId));

    public List<PropertyMutationOperationRecord> Added { get; } = [];

    public int ReadCount { get; private set; }

    public Task<PropertyMutationOperationRecord?> GetAsync(
        PropertyMutationResourceKind resourceKind,
        Guid resourceId,
        Guid operationId,
        CancellationToken cancellationToken)
    {
        this.ReadCount++;
        this.operations.TryGetValue(
            (resourceKind, resourceId, operationId),
            out PropertyMutationOperationRecord? operation);
        return Task.FromResult(operation);
    }

    public Task AddAsync(
        PropertyMutationOperationRecord operation,
        CancellationToken cancellationToken)
    {
        this.operations.Add(
            (
                operation.ResourceKind,
                operation.ResourceId,
                operation.OperationId),
            operation);
        this.Added.Add(operation);
        return Task.CompletedTask;
    }
}
