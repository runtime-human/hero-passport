using Xunit;

namespace HeroPassport.AgentEvals;

public sealed class ReleasePlatformMatrixContractTests
{
    [Fact]
    public void ReleaseQualificationUsesLeanStandardRunnerMatrixWithoutDuplicatingCodexHostWork()
    {
        var root = RepoRoot();
        var primaryPath = Path.Combine(root, ".github", "workflows", "ci.yml");
        var releasePath = Path.Combine(root, ".github", "workflows", "release-platform.yml");

        Assert.True(File.Exists(releasePath), $"Missing release platform workflow: {releasePath}");

        var primary = File.ReadAllText(primaryPath);
        var release = File.ReadAllText(releasePath);

        Assert.Contains("runs-on: ubuntu-24.04", primary, StringComparison.Ordinal);
        Assert.Contains("concurrency:", primary, StringComparison.Ordinal);
        Assert.Contains("cancel-in-progress: true", primary, StringComparison.Ordinal);

        Assert.Contains("windows-2025", release, StringComparison.Ordinal);
        Assert.Contains("macos-15", release, StringComparison.Ordinal);
        Assert.Contains("fail-fast: false", release, StringComparison.Ordinal);
        Assert.DoesNotContain("ubuntu-24.04", release, StringComparison.Ordinal);

        Assert.Contains("tests/HeroPassport.Infrastructure.Tests/HeroPassport.Infrastructure.Tests.csproj", release, StringComparison.Ordinal);
        Assert.Contains("dotnet publish src/HeroPassport.App/HeroPassport.App.csproj", release, StringComparison.Ordinal);
        Assert.Contains("tests/HeroPassport.PackagedE2E/HeroPassport.PackagedE2E.csproj", release, StringComparison.Ordinal);
        Assert.Contains("HERO_PASSPORT_PUBLISH_DIR", release, StringComparison.Ordinal);

        Assert.Contains("workflow_dispatch:", release, StringComparison.Ordinal);
        Assert.Contains("paths:", release, StringComparison.Ordinal);
        Assert.Contains("concurrency:", release, StringComparison.Ordinal);
        Assert.Contains("cancel-in-progress: true", release, StringComparison.Ordinal);

        Assert.DoesNotContain("codex-host-config-smoke.sh", release, StringComparison.Ordinal);
        Assert.DoesNotContain("upload-artifact", release, StringComparison.Ordinal);
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HeroPassport.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Hero Passport repository root was not found from the AgentEvals output directory.");
    }
}
