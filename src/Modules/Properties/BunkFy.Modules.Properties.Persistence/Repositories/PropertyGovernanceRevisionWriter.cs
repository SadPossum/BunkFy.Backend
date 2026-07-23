namespace BunkFy.Modules.Properties.Persistence.Repositories;

using BunkFy.Modules.Properties.Application.Ports;

internal sealed class PropertyGovernanceRevisionWriter(PropertiesDbContext dbContext)
    : IPropertyGovernanceRevisionWriter
{
    public async Task AppendAsync(
        PropertyGovernanceRevisionWriteModel revision,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(revision);
        await dbContext.GovernanceRevisions.AddAsync(
            new PropertyGovernanceRevision(revision),
            cancellationToken).ConfigureAwait(false);
    }
}
