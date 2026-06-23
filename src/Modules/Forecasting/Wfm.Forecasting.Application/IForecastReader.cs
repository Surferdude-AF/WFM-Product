using Wfm.Forecasting.Domain;

namespace Wfm.Forecasting.Application;

// The latest persisted forecast for a Skill (UTC), ordered by interval. Empty when
// the Skill has not been forecast yet.
public interface IForecastReader
{
    Task<IReadOnlyList<ForecastPoint>> ForSkillAsync(SkillId skill, CancellationToken cancellationToken = default);
}
