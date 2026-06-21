namespace Wfm.IntegrationTests;

// A [Fact] that skips (rather than fails) when no Docker engine is reachable, so
// the Testcontainers suite runs on CI but doesn't block dev machines that haven't
// installed Docker yet (scaffolding-plan: Docker is a Phase B prerequisite).
public sealed class DockerFactAttribute : FactAttribute
{
    public DockerFactAttribute()
    {
        if (!DockerEnvironment.IsAvailable)
        {
            Skip = "Docker engine not reachable; skipping Testcontainers integration test.";
        }
    }
}

internal static class DockerEnvironment
{
    public static bool IsAvailable { get; } = Detect();

    private static bool Detect()
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOCKER_HOST")))
        {
            return true;
        }

        if (OperatingSystem.IsWindows())
        {
            try
            {
                return Directory.EnumerateFiles(@"\\.\pipe\")
                    .Any(pipe => pipe.Contains("docker_engine", StringComparison.OrdinalIgnoreCase));
            }
            catch (IOException)
            {
                return false;
            }
        }

        return File.Exists("/var/run/docker.sock");
    }
}
