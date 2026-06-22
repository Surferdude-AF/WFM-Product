using CsCheck;
using Wfm.Forecasting.Domain;

namespace Wfm.Forecasting.Domain.Tests;

public class ErlangAPropertyTests
{
    [Fact]
    public void No_contacts_needs_no_agents()
    {
        Assert.Equal(0, ErlangA.RequiredAgents(0, 300, 60, 0.80, 20, 0.85));
    }

    [Fact]
    public void More_contacts_never_needs_fewer_agents()
    {
        Gen.Select(Gen.Int[0, 1500], Gen.Int[0, 1500], Gen.Int[60, 600]).Sample(t =>
        {
            var (a, b, aht) = t;
            var lower = Math.Min(a, b);
            var higher = Math.Max(a, b);

            return ErlangA.RequiredAgents(lower, aht, 60, 0.80, 20, 0.85)
                <= ErlangA.RequiredAgents(higher, aht, 60, 0.80, 20, 0.85);
        });
    }

    [Fact]
    public void Service_level_is_non_decreasing_in_agents()
    {
        Gen.Select(Gen.Int[1, 800], Gen.Int[60, 600], Gen.Int[10, 300], Gen.Int[1, 60], Gen.Int[0, 60])
            .Sample(t =>
            {
                var (contacts, aht, patience, within, extra) = t;
                // Stay in the sensible regime: at or above the offered load.
                var offered = contacts / 900.0 * aht;
                var c = Math.Max(1, (int)Math.Ceiling(offered)) + extra;

                var here = ErlangA.ServiceLevel(c, contacts, aht, patience, within);
                var next = ErlangA.ServiceLevel(c + 1, contacts, aht, patience, within);
                return next >= here - 1e-9;
            });
    }

    [Fact]
    public void Service_level_never_exceeds_one()
    {
        // SL = 1 - P(wait)*e^(...) and the subtracted term is non-negative, so SL <= 1
        // for any agent count (it can go negative under severe understaffing).
        Gen.Select(Gen.Int[1, 1000], Gen.Int[60, 600], Gen.Int[10, 300], Gen.Int[1, 300], Gen.Int[1, 60])
            .Sample(t =>
            {
                var (contacts, aht, patience, agents, within) = t;
                return ErlangA.ServiceLevel(agents, contacts, aht, patience, within) <= 1 + 1e-9;
            });
    }

    [Fact]
    public void Service_level_is_a_valid_fraction_when_adequately_staffed()
    {
        Gen.Select(Gen.Int[1, 1000], Gen.Int[60, 600], Gen.Int[10, 300], Gen.Int[1, 60], Gen.Int[0, 80])
            .Sample(t =>
            {
                var (contacts, aht, patience, within, extra) = t;
                var offered = contacts / 900.0 * aht;
                var agents = Math.Max(1, (int)Math.Ceiling(offered)) + extra;

                var sl = ErlangA.ServiceLevel(agents, contacts, aht, patience, within);
                return sl is >= -1e-9 and <= 1 + 1e-9;
            });
    }

    [Fact]
    public void Probability_of_waiting_is_a_probability()
    {
        Gen.Select(Gen.Int[1, 300], Gen.Double[0.1, 250.0], Gen.Double[0.0, 5.0])
            .Sample(t =>
            {
                var (agents, offered, gamma) = t;
                var p = ErlangA.ProbabilityWait(agents, offered, gamma);
                return p is >= -1e-9 and <= 1 + 1e-9;
            });
    }
}
