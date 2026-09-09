using Xunit;

namespace HeroPassport.AgentEvals;

public sealed class CodexHostRuntimeSmokeContractTests
{
    [Fact]
    public void QualificationRunsRealCodexExecAndCapturesHeroPassportMcpTools()
    {
        var root = RepoRoot();
        var harnessPath = Path.Combine(root, "tests", "qualification", "codex-host-runtime-smoke.py");
        Assert.True(File.Exists(harnessPath), $"Missing Codex runtime smoke harness: {harnessPath}");

        var harness = File.ReadAllText(harnessPath);
        Assert.Contains("codex", harness, StringComparison.Ordinal);
        Assert.Contains("exec", harness, StringComparison.Ordinal);
        Assert.Contains("/v1/responses", harness, StringComparison.Ordinal);
        Assert.Contains("response.completed", harness, StringComparison.Ordinal);

        string[] expectedTools =
        [
            "hero.bootstrap",
            "hero.configure",
            "hero.get_context",
            "hero.create",
            "hero.list",
            "hero.activate",
            "hero.archive",
            "hero.restore",
            "hero.start_quest",
            "hero.finish_quest",
            "hero.get_card",
        ];

        foreach (var tool in expectedTools)
        {
            Assert.Contains(tool, harness, StringComparison.Ordinal);
        }

        var smokePath = Path.Combine(root, "tests", "qualification", "codex-host-config-smoke.sh");
        var smoke = File.ReadAllText(smokePath);
        Assert.Contains("codex-host-runtime-smoke.py", smoke, StringComparison.Ordinal);

        var workflowPath = Path.Combine(root, ".github", "workflows", "ci.yml"));
        Assert.Contains("Codex host runtime smoke", workflow, StringComparison.Ordinal);
        Assert.Contains("bash tests/qualification/codex-host-config-smoke.sh", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void QualificationExecutesReadOnlyContextThroughCodexMcpRoundTrip()
    {
        var path = Path.Combine(RepoRoot(), "tests", "qualification", "codex-host-runtime-smoke.py");
        Assert.True(File.Exists(path), $"Missing Codex runtime smoke harness: {path}");

        var harness = File.ReadAllText(path);
        Assert.Contains("call-hero-get-context", harness, StringComparison.Ordinal);
        Assert.Contains("function_call", harness, StringComparison.Ordinal);
        Assert.Contains("mcp__hero_passport", harness, StringComparison.Ordinal);
        Assert.Contains("hero_get_context", harness, StringComparison.Ordinal);
        Assert.Contains("function_call_output", harness, StringComparison.Ordinal);
        Assert.Contains("setupCompleted", harness, StringComparison.Ordinal);
        Assert.Contains("Hero Passport setup is required.", harness, StringComparison.Ordinal);
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
