using Wfm.Forecasting.Application;
using Wfm.Forecasting.Domain;
using Wfm.SharedKernel;

namespace Wfm.Forecasting.Infrastructure;

public sealed class InMemoryForecastStreamReader : IForecastStreamReader
{
    public Task<bool> ExistsAsync(TenantId tenant, SkillId skill, CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}
