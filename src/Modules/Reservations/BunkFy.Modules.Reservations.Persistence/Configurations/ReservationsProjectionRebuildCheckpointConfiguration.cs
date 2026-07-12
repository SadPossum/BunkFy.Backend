namespace BunkFy.Modules.Reservations.Persistence.Configurations;

using Gma.Framework.ProjectionRebuild.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class ReservationsProjectionRebuildCheckpointConfiguration
    : IEntityTypeConfiguration<ReservationsProjectionRebuildCheckpoint>
{
    public void Configure(EntityTypeBuilder<ReservationsProjectionRebuildCheckpoint> builder) =>
        builder.ConfigureProjectionRebuildCheckpointState();
}
