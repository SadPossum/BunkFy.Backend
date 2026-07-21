namespace BunkFy.Modules.Workspaces.Application.Ports;

using BunkFy.Modules.Properties.Contracts;

public interface IWorkspacePropertyProjectionRepository
{
    Task<bool> AreAllActiveAsync(
        IReadOnlyCollection<Guid> propertyIds,
        CancellationToken cancellationToken);

    Task ApplyAsync(
        WorkspacePropertyProjectionWriteModel property,
        CancellationToken cancellationToken);
}

public sealed record WorkspacePropertyProjectionWriteModel(
    string ScopeId,
    Guid PropertyId,
    string Name,
    PropertyStatus Status,
    long Version);
