namespace BunkFy.Modules.Guests.Persistence.Configurations;

using Gma.Framework.ProjectionRebuild.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class GuestsProjectionRebuildCheckpointConfiguration
    : IEntityTypeConfiguration<GuestsProjectionRebuildCheckpoint>
{
    public void Configure(EntityTypeBuilder<GuestsProjectionRebuildCheckpoint> builder) =>
        builder.ConfigureProjectionRebuildCheckpointState();
}
