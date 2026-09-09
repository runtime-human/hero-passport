using Xunit;

namespace HeroPassport.AgentEvals;

public sealed class CodexHostLifecycleSmokeContractTests
{
    [Fact]
    public void QualificationRunsPersistentHeroLifecycleAcrossRealCodexRestart()
    {
        var root = RepoRoot();
        var harnessPath = Path.Combine(root, "tests", "qualification", "codex-host-lifecycle-smoke.py");
        Assert.True(File.Exists(harnessPath), $"Missing Codex lifecycle qualification harness: {harnessPath}");

        var harness = File.ReadAllText(harnessPath);
        Assert.Contains("hero_bootstrap", harness, StringComparison.Ordinal);
        Assert.Contains("hero_start_quest", harness, StringComparison.Ordinal);
        Assert.Contains("hero_finish_quest", harness, StringComparison.Ordinal);
        Assert.Contains("hero_get_context", harness, StringComparison.Ordinal);
        Assert.Contains("hero_get_card", harness, StringComparison.Ordinal);
        Assert.Contains("replayed", harness, StringComparison.Ordinal);
        Assert.Contains("HERO_PASSPORT_LIFECYCLE_PHASE1", harness, StringComparison.Ordinal);
        Assert.Contains("HERO_PASSPORT_LIFECYCLE_REPLAY", harness, StringComparison.Ordinal);
        Assert.Contains("subprocess.run", harness, StringComparison.Ordinal);

        var smokePath = Path.Combine(root, "tests", "qualification", "codex-host-config-smoke.sh");
        var smoke = File.ReadAllText(smokePath);
        Assert.Contains("HERO_PASSPORT_HOME", smoke, StringComparison.Ordinal);
        Assert.Contains("codex-host-lifecycle-smoke.py", smoke, StringComparison.Ordinal);

        var workflowPath = Path.Combine(root, ".github", "workflows", "ci.yml");
        var workflow = File.ReadAllText(workflowPath);
        Assert.Contains("Codex host configuration smoke / Codex host runtime smoke / lifecycle restart smoke", workflow, StringComparison.Ordinal);
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
