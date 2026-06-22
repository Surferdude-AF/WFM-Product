using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wfm.SharedKernel;

namespace Wfm.Forecasting.Infrastructure.Persistence.Configurations;

internal sealed class QueueConfiguration : IEntityTypeConfiguration<Queue>
{
    public void Configure(EntityTypeBuilder<Queue> builder)
    {
        builder.ToTable("queues");
        builder.HasKey(q => q.Id);

        builder.Property(q => q.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new QueueId(value));

        builder.Property(q => q.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(id => id.Value, value => new TenantId(value));

        builder.Property(q => q.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.HasIndex(q => q.TenantId);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(q => q.TenantId);
    }
}
