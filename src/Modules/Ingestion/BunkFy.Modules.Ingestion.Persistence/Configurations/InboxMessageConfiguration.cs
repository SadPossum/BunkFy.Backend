namespace BunkFy.Modules.Ingestion.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Naming;

internal sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
        => builder.ConfigureInboxMessage();
}
