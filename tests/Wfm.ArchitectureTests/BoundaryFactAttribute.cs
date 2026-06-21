using System.Reflection;

namespace Wfm.ArchitectureTests;

// A [Fact] for NetArchTest boundary checks that skips when the runtime is not
// permitted to reflectively load the unsigned module assemblies these checks
// inspect -- e.g. Windows Smart App Control blocks such loads on dev machines.
// The boundary gate is enforced in CI (Linux), where the load is permitted.
public sealed class BoundaryFactAttribute : FactAttribute
{
    public BoundaryFactAttribute()
    {
        if (!ModuleAssemblyLoading.IsPermitted)
        {
            Skip = "Reflective load of unsigned module assemblies is blocked here "
                + "(e.g. Smart App Control); boundary checks are enforced in CI.";
        }
    }
}

internal static class ModuleAssemblyLoading
{
    public static bool IsPermitted { get; } = Probe();

    private static bool Probe()
    {
        try
        {
            // Load by name (resolved at runtime, inside the try) so a blocking
            // policy surfaces here rather than failing the test body.
            _ = Assembly.Load("Wfm.SharedKernel").GetTypes();
            return true;
        }
        catch (Exception ex) when (ex is FileLoadException or BadImageFormatException or TypeLoadException)
        {
            return false;
        }
    }
}
