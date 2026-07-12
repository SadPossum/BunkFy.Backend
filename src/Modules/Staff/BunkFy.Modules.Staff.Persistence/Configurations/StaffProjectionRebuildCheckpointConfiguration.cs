namespace BunkFy.Modules.Staff.Persistence.Configurations;

using Gma.Framework.ProjectionRebuild.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class StaffProjectionRebuildCheckpointConfiguration
    : IEntityTypeConfiguration<StaffProjectionRebuildCheckpoint>
{
    public void Configure(EntityTypeBuilder<StaffProjectionRebuildCheckpoint> builder) =>
        builder.ConfigureProjectionRebuildCheckpointState();
}
