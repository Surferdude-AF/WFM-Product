using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wfm.Forecasting.Domain;
using Wfm.SharedKernel;

namespace Wfm.Forecasting.Infrastructure.Persistence.Configurations;

internal sealed class ForecastJobConfiguration : IEntityTypeConfiguration<ForecastJob>
{
    public void Configure(EntityTypeBuilder<ForecastJob> builder)
    {
        builder.ToTable("forecast_jobs");
        builder.HasKey(j => j.Id);

        builder.Property(j => j.Id).HasColumnName("id");

        builder.Property(j => j.SkillId)
            .HasColumnName("skill_id")
            .HasConversion(id => id.Value, value => new SkillId(value));

        builder.Property(j => j.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(id => id.Value, value => new TenantId(value));

        builder.Property(j => j.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
        builder.Property(j => j.RequestedAt).HasColumnName("requested_at");
        builder.Property(j => j.CompletedAt).HasColumnName("completed_at");

        builder.HasIndex(j => j.Status);

        builder.HasOne<Skill>().WithMany().HasForeignKey(j => j.SkillId);
    }
}
