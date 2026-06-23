using Wfm.Forecasting.Domain;

namespace Wfm.Forecasting.Application;

// Requests a forecast for a Skill on the current tenant by enqueuing a job; the
// worker picks it up and runs the pipeline (step 11b).
public interface IForecastTrigger
{
    Task EnqueueAsync(SkillId skill, CancellationToken cancellationToken = default);
}
