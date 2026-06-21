using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wfm.Forecasting.Domain;
using Wfm.SharedKernel;

namespace Wfm.Forecasting.Infrastructure.Persistence.Configurations;

internal sealed class SkillConfiguration : IEntityTypeConfiguration<Skill>
{
    public void Configure(EntityTypeBuilder<Skill> builder)
    {
        builder.ToTable("skills");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new SkillId(value));

        builder.Property(s => s.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(id => id.Value, value => new TenantId(value));

        builder.Property(s => s.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.HasIndex(s => s.TenantId);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(s => s.TenantId);
    }
}
