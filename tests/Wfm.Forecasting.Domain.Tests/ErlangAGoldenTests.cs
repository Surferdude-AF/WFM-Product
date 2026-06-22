using Wfm.Forecasting.Domain;

namespace Wfm.Forecasting.Domain.Tests;

// Golden / characterisation (ADR-006 layer 2): the staffing curve and the raw
// service-level math must reproduce the prototype across a representative grid
// (including occupancy-cap and fallback cases) -- the safety net for the
// log-space probability sum.
public class ErlangAGoldenTests
{
    [Fact]
    public void Reproduces_the_prototype_staffing_grid()
    {
        foreach (var row in Fixtures.LoadErlang().Staffing)
        {
            var agents = ErlangA.RequiredAgents(
                row.Contacts, row.Aht, row.Patience, row.SlPct / 100.0, row.SlSecs, row.OccCap);

            Assert.Equal(row.Agents, agents);
        }
    }

    [Fact]
    public void Reproduces_the_prototype_service_levels()
    {
        foreach (var point in Fixtures.LoadErlang().ServiceLevel)
        {
            var sl = ErlangA.ServiceLevel(point.Agents, point.Contacts, point.Aht, point.Patience, point.WithinSecs);

            Assert.Equal(point.Sl, sl, precision: 9);
        }
    }
}
