using Microsoft.EntityFrameworkCore;
using Wfm.Forecasting.Domain;

namespace Wfm.Forecasting.Infrastructure.Persistence;

public sealed class WfmDbContext(DbContextOptions<WfmDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Skill> Skills => Set<Skill>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WfmDbContext).Assembly);
    }
}
