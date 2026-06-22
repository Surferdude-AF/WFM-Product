using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wfm.Forecasting.Domain;

namespace Wfm.Forecasting.Infrastructure.Persistence.Configurations;

internal sealed class SkillIntervalStatConfiguration : IEntityTypeConfiguration<SkillIntervalStat>
{
    public void Configure(EntityTypeBuilder<SkillIntervalStat> builder)
    {
        // Keyless read model mapped to the SQL view (created in the migration, not
        // by EF). Excluded from migrations -- EF never tries to create a table.
        builder.HasNoKey();
        builder.ToView("skill_interval_stats");

        builder.Property(s => s.SkillId)
            .HasColumnName("skill_id")
            .HasConversion(id => id.Value, value => new SkillId(value));

        builder.Property(s => s.IntervalStart).HasColumnName("interval_start");
        builder.Property(s => s.Contacts).HasColumnName("contacts");
        builder.Property(s => s.AhtSeconds).HasColumnName("aht_seconds");
    }
}
