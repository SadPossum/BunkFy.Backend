namespace BunkFy.Modules.Workspaces.Persistence.Configurations;

using Gma.Framework.ProjectionRebuild.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class WorkspaceProjectionRebuildCheckpointConfiguration
    : IEntityTypeConfiguration<WorkspaceProjectionRebuildCheckpoint>
{
    public void Configure(EntityTypeBuilder<WorkspaceProjectionRebuildCheckpoint> builder) =>
        builder.ConfigureProjectionRebuildCheckpointState();
}
