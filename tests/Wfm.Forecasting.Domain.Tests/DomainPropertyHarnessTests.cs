using CsCheck;

namespace Wfm.Forecasting.Domain.Tests;

public class DomainPropertyHarnessTests
{
    // Harness smoke test: proves the property-based (CsCheck) pipeline runs in CI.
    // Real forecast invariants replace this in Phase C (scaffolding-plan step 9).
    [Fact]
    public void Property_pipeline_runs()
    {
        Gen.Int.Sample(i => i + 0 == i);
    }
}
