using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wfm.Forecasting.Domain;
using Wfm.SharedKernel;

namespace Wfm.Forecasting.Infrastructure.Persistence.Configurations;

internal sealed class SkillQueueConfiguration : IEntityTypeConfiguration<SkillQueue>
{
    public void Configure(EntityTypeBuilder<SkillQueue> builder)
    {
        builder.ToTable("skill_queues");
        builder.HasKey(sq => new { sq.SkillId, sq.QueueId });

        builder.Property(sq => sq.SkillId)
            .HasColumnName("skill_id")
            .HasConversion(id => id.Value, value => new SkillId(value));

        builder.Property(sq => sq.QueueId)
            .HasColumnName("queue_id")
            .HasConversion(id => id.Value, value => new QueueId(value));

        builder.Property(sq => sq.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(id => id.Value, value => new TenantId(value));

        builder.HasIndex(sq => sq.TenantId);

        builder.HasOne<Skill>().WithMany().HasForeignKey(sq => sq.SkillId);
        builder.HasOne<Queue>().WithMany().HasForeignKey(sq => sq.QueueId);
    }
}
