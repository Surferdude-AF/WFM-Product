using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wfm.SharedKernel;

namespace Wfm.Forecasting.Infrastructure.Persistence.Configurations;

internal sealed class QueueIntervalStatConfiguration : IEntityTypeConfiguration<QueueIntervalStat>
{
    public void Configure(EntityTypeBuilder<QueueIntervalStat> builder)
    {
        builder.ToTable("queue_interval_stats");

        // One stat per Queue per interval -- the natural key, and the upsert target.
        builder.HasKey(s => new { s.QueueId, s.IntervalStart });

        builder.Property(s => s.QueueId)
            .HasColumnName("queue_id")
            .HasConversion(id => id.Value, value => new QueueId(value));

        builder.Property(s => s.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(id => id.Value, value => new TenantId(value));

        builder.Property(s => s.IntervalStart).HasColumnName("interval_start");
        builder.Property(s => s.Contacts).HasColumnName("contacts");
        builder.Property(s => s.AhtSeconds).HasColumnName("aht_seconds");

        builder.HasIndex(s => s.TenantId);

        builder.HasOne<Queue>()
            .WithMany()
            .HasForeignKey(s => s.QueueId);
    }
}
