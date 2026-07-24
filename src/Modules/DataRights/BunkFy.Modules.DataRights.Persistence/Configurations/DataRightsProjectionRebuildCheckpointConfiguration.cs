namespace BunkFy.Modules.DataRights.Persistence.Configurations;

using Gma.Framework.ProjectionRebuild.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class DataRightsProjectionRebuildCheckpointConfiguration
    : IEntityTypeConfiguration<DataRightsProjectionRebuildCheckpoint>
{
    public void Configure(EntityTypeBuilder<DataRightsProjectionRebuildCheckpoint> builder) =>
        builder.ConfigureProjectionRebuildCheckpointState();
}
