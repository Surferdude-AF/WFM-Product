using System.Diagnostics;

namespace Wfm.AcceptanceTests;

// A [Fact] that skips (rather than fails) when no Docker engine is reachable, so
// the Testcontainers-backed acceptance suite runs on CI but doesn't block dev
// machines without Docker (scaffolding-plan: Docker is a Phase B prerequisite).
public sealed class DockerFactAttribute : FactAttribute
{
    public DockerFactAttribute()
    {
        if (!DockerEnvironment.IsAvailable)
        {
            Skip = "Docker engine not reachable; skipping Testcontainers acceptance test.";
        }
    }
}

internal static class DockerEnvironment
{
    public static bool IsAvailable { get; } = Detect();

    private static bool Detect()
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo("docker")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            process.StartInfo.ArgumentList.Add("version");
            process.StartInfo.ArgumentList.Add("--format");
            process.StartInfo.ArgumentList.Add("{{.Server.Version}}");

            if (!process.Start())
            {
                return false;
            }

            if (!process.WaitForExit(TimeSpan.FromSeconds(10)))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
                return false;
            }

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
