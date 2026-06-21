using NetArchTest.Rules;
using Wfm.Forecasting.Application;
using Wfm.Forecasting.Domain;
using Wfm.Forecasting.Infrastructure;
using Wfm.SharedKernel;

namespace Wfm.ArchitectureTests;

// Enforces the ADR-007 module boundaries: an acyclic graph whose dependencies
// point inward toward the pure domain core (ADR-005). A forbidden reference
// fails these tests, which fails CI.
public class BoundaryTests
{
    private const string Domain = "Wfm.Forecasting.Domain";
    private const string Application = "Wfm.Forecasting.Application";
    private const string Infrastructure = "Wfm.Forecasting.Infrastructure";
    private const string Api = "Wfm.Api";
    private const string SharedKernel = "Wfm.SharedKernel";

    [BoundaryFact]
    public void Domain_depends_on_nothing_in_the_solution()
    {
        var result = Types.InAssembly(typeof(SkillId).Assembly)
            .Should()
            .NotHaveDependencyOnAny(SharedKernel, Application, Infrastructure, Api)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [BoundaryFact]
    public void Domain_is_framework_free()
    {
        var result = Types.InAssembly(typeof(SkillId).Assembly)
            .Should()
            .NotHaveDependencyOnAny(
                "Microsoft.AspNetCore",
                "Microsoft.EntityFrameworkCore",
                "Npgsql",
                "Microsoft.Extensions")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [BoundaryFact]
    public void Application_does_not_depend_on_infrastructure_or_host()
    {
        var result = Types.InAssembly(typeof(IForecastStreamReader).Assembly)
            .Should()
            .NotHaveDependencyOnAny(Infrastructure, Api)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [BoundaryFact]
    public void SharedKernel_does_not_depend_on_any_module()
    {
        var result = Types.InAssembly(typeof(TenantId).Assembly)
            .Should()
            .NotHaveDependencyOnAny(Domain, Application, Infrastructure, Api)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [BoundaryFact]
    public void Infrastructure_does_not_depend_on_the_host()
    {
        var result = Types.InAssembly(typeof(InMemoryForecastStreamReader).Assembly)
            .Should()
            .NotHaveDependencyOn(Api)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    private static string Describe(TestResult result) =>
        result.IsSuccessful
            ? string.Empty
            : "Forbidden dependency in: " + string.Join(", ", result.FailingTypeNames ?? []);
}
