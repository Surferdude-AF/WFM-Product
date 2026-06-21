using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Wfm.Forecasting.Infrastructure.Persistence;

// Lets `dotnet ef` build the context at design time without the API host.
// This connection string is only used to scaffold migrations, never at runtime.
internal sealed class WfmDbContextFactory : IDesignTimeDbContextFactory<WfmDbContext>
{
    public WfmDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<WfmDbContext>()
            .UseNpgsql("Host=localhost;Database=wfm;Username=postgres;Password=postgres")
            .Options;

        return new WfmDbContext(options);
    }
}
