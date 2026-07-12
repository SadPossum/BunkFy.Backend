namespace BunkFy.Modules.Ingestion.Persistence.Configurations;

using Gma.Framework.ProjectionRebuild.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class IngestionProjectionRebuildCheckpointConfiguration
    : IEntityTypeConfiguration<IngestionProjectionRebuildCheckpoint>
{
    public void Configure(EntityTypeBuilder<IngestionProjectionRebuildCheckpoint> builder) =>
        builder.ConfigureProjectionRebuildCheckpointState();
}
