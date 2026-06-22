using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wfm.Forecasting.Domain;
using Wfm.SharedKernel;

namespace Wfm.Forecasting.Infrastructure.Persistence.Configurations;

internal sealed class SkillForecastConfiguration : IEntityTypeConfiguration<SkillForecast>
{
    public void Configure(EntityTypeBuilder<SkillForecast> builder)
    {
        builder.ToTable("skill_forecasts");
        builder.HasKey(f => new { f.SkillId, f.IntervalStart });

        builder.Property(f => f.SkillId)
            .HasColumnName("skill_id")
            .HasConversion(id => id.Value, value => new SkillId(value));

        builder.Property(f => f.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(id => id.Value, value => new TenantId(value));

        builder.Property(f => f.IntervalStart).HasColumnName("interval_start");
        builder.Property(f => f.Contacts).HasColumnName("contacts");
        builder.Property(f => f.AhtSeconds).HasColumnName("aht_seconds");
        builder.Property(f => f.GeneratedAt).HasColumnName("generated_at");

        builder.HasIndex(f => f.TenantId);

        builder.HasOne<Skill>().WithMany().HasForeignKey(f => f.SkillId);
    }
}
