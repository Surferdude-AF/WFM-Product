using Wfm.Forecasting.Domain;

namespace Wfm.Forecasting.Application;

// Runs the forecast pipeline for one Skill and persists the result: read the
// Skill's aggregated stats, exclude outlier days, forecast in the Skill's timezone,
// and store the forecast week. The forecast week is derived from the data, not the
// clock (ADR-006 determinism).
public interface IForecastService
{
    Task ForecastSkillAsync(SkillId skill, CancellationToken cancellationToken = default);
}
