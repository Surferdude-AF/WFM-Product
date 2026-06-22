namespace Wfm.Forecasting.Domain;

// Erlang A staffing (M/M/c+M, Palm's formula): like Erlang C but models impatient
// callers who abandon at rate theta (reduces to Erlang C as patience -> infinity).
// Pure and deterministic. The wait probability is summed in log-space for numerical
// stability at high agent counts -- the golden fixture guards that arithmetic.
public static class ErlangA
{
    private const int IntervalSeconds = 900; // a 15-minute interval
    private const int MaxAgents = 500;

    // P(an arriving caller must wait), given agents c, offered load a (Erlangs),
    // and gamma = AHT/patience.
    public static double ProbabilityWait(int agents, double offeredLoad, double gamma)
    {
        var terms = agents + 200;
        var logP = new List<double>(terms + 1) { 0.0 };
        for (var n = 1; n <= terms; n++)
        {
            var prev = logP[n - 1];
            double value;
            if (n <= agents)
            {
                value = prev + Math.Log(offeredLoad) - Math.Log(n);
            }
            else
            {
                var denominator = agents + ((n - agents) * gamma);
                if (denominator <= 0)
                {
                    break;
                }

                value = prev + Math.Log(offeredLoad) - Math.Log(denominator);
            }

            logP.Add(value);
            if (value < logP[0] - 50 && n > agents)
            {
                break;
            }
        }

        var maxLog = logP.Max();
        var total = 0.0;
        var waiting = 0.0;
        for (var i = 0; i < logP.Count; i++)
        {
            var p = Math.Exp(logP[i] - maxLog);
            total += p;
            if (i >= agents)
            {
                waiting += p;
            }
        }

        return waiting / total;
    }

    // Fraction of callers answered within `withinSeconds`, for `agents` agents.
    public static double ServiceLevel(int agents, int contacts, int ahtSeconds, double patienceSeconds, double withinSeconds)
    {
        var lambda = (double)contacts / IntervalSeconds;
        var mu = 1.0 / ahtSeconds;
        var theta = 1.0 / patienceSeconds;
        var offeredLoad = lambda / mu;
        var gamma = theta / mu;

        var probabilityWait = ProbabilityWait(agents, offeredLoad, gamma);
        return 1 - (probabilityWait * Math.Exp(-mu * (agents - offeredLoad + (agents * gamma)) * withinSeconds));
    }

    // Fewest agents meeting the service-level target AND the occupancy cap.
    public static int RequiredAgents(
        int contacts,
        int ahtSeconds,
        double patienceSeconds,
        double serviceLevelTarget,
        double serviceLevelSeconds,
        double occupancyCap)
    {
        if (contacts <= 0)
        {
            return 0;
        }

        var offeredLoad = (double)contacts / IntervalSeconds * ahtSeconds;
        for (var agents = Math.Max(1, (int)Math.Ceiling(offeredLoad)); agents <= MaxAgents; agents++)
        {
            if (offeredLoad / agents > occupancyCap)
            {
                continue;
            }

            if (ServiceLevel(agents, contacts, ahtSeconds, patienceSeconds, serviceLevelSeconds) >= serviceLevelTarget)
            {
                return agents;
            }
        }

        return (int)Math.Ceiling(offeredLoad / occupancyCap);
    }
}
