namespace SlimMessageBus.Host.Outbox.DbContext;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class OutboxMessageEntityTypeConfiguration(DbContextOutboxSettings outboxSettings) : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable(outboxSettings.DatabaseTableName, outboxSettings.DatabaseSchemaName);

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.Timestamp)
            .IsRequired();

        builder.Property(x => x.BusName)
            .HasMaxLength(64)
            .IsRequired(false);

        builder.Property(x => x.MessageType)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.MessagePayload)
            .IsRequired();

        builder.Property(x => x.Headers)
            .IsRequired(false);

        builder.Property(x => x.Path)
            .HasMaxLength(128)
            .IsRequired(false);

        builder.Property(x => x.InstanceId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.LockInstanceId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.LockExpiresOn)
            .IsRequired();

        builder.Property(x => x.DeliveryAttempt)
            .IsRequired();

        builder.Property(x => x.DeliveryComplete)
            .IsRequired();

        builder.Property(x => x.DeliveryAborted)
            .HasDefaultValue(false)
            .IsRequired();

        builder.HasIndex(x => new { x.Timestamp, x.LockInstanceId, x.LockExpiresOn })
            .HasFilter("DeliveryComplete = 0 and DeliveryAborted = 0")
            .HasNameInternal("IX_Outbox_Timestamp_LockInstanceId_LockExpiresOn_DeliveryComplete_0_DeliveryAborted_0");

        builder.HasIndex(x => new { x.LockExpiresOn, x.LockInstanceId })
            .HasFilter("DeliveryComplete = 0 and DeliveryAborted = 0")
            .HasNameInternal("IX_Outbox_LockExpiresOn_LockInstanceId_DeliveryComplete_0_DeliveryAborted_0");

        builder.HasIndex(x => new { x.Timestamp })
            .HasFilter("DeliveryComplete = 1 and DeliveryAborted = 0")
            .HasNameInternal("IX_Outbox_Timestamp_DeliveryComplete_1_DeliveryAborted_0");
    }
}

static internal class IndexBuilderExtensions
{
    static internal IndexBuilder<TEntity> HasNameInternal<TEntity>(this IndexBuilder<TEntity> indexBuilder, string name)
    {
#if NETSTANDARD2_0
        return indexBuilder.HasName(name);
#else
        return indexBuilder.HasDatabaseName(name);
#endif
    }
}
