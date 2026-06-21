using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wfm.SharedKernel;

namespace Wfm.Forecasting.Infrastructure.Persistence.Configurations;

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new TenantId(value));

        builder.Property(t => t.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();
    }
}
