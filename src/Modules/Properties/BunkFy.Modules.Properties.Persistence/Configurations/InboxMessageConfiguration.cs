namespace BunkFy.Modules.Properties.Persistence.Configurations;

using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Naming;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder) =>
        builder.ConfigureInboxMessage();
}
