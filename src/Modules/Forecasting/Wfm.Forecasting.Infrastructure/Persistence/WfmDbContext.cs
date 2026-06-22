using Microsoft.EntityFrameworkCore;
using Wfm.Forecasting.Domain;

namespace Wfm.Forecasting.Infrastructure.Persistence;

public sealed class WfmDbContext(DbContextOptions<WfmDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<Queue> Queues => Set<Queue>();
    public DbSet<QueueIntervalStat> QueueIntervalStats => Set<QueueIntervalStat>();
    public DbSet<SkillQueue> SkillQueues => Set<SkillQueue>();
    public DbSet<SkillIntervalStat> SkillIntervalStats => Set<SkillIntervalStat>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WfmDbContext).Assembly);
    }
}
