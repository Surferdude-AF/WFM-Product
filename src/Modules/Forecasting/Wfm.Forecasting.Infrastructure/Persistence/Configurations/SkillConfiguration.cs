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

        builder.Property(s => s.TimeZoneId)
            .HasColumnName("time_zone_id")
            .HasMaxLength(100);

        // Variable-shape config stored as jsonb (ADR-002); the JSON null literal is
        // always-open, which is also the column default so existing rows need no backfill.
        builder.Property(s => s.OperatingHours)
            .HasColumnName("operating_hours")
            .HasColumnType("jsonb")
            .HasConversion(h => OperatingHoursJson.ToJson(h), j => OperatingHoursJson.FromJson(j));

        builder.HasIndex(s => s.TenantId);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(s => s.TenantId);
    }
}
