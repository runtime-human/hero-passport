using Xunit;

namespace HeroPassport.AgentEvals;

public sealed class CodexHostSmokeContractTests
{
    [Fact]
    public void CiRunsPinnedOfficialCodexAgainstPackagedHeroPassportConfiguration()
    {
        var repoRoot = RepoRoot();
        var scriptPath = Path.Combine(repoRoot, "tests", "qualification", "codex-host-config-smoke.sh");
        Assert.True(File.Exists(scriptPath), $"Missing Codex host smoke script: {scriptPath}");

        var script = File.ReadAllText(scriptPath);
        Assert.Contains("CODEX_VERSION=0.153.4", script, StringComparison.Ordinal);
        Assert.Contains("f479424eca092484dc40d87ae28c44f4cc40234a60045d6131e493800d814a30", script, StringComparison.Ordinal);
        Assert.Contains("openai/codex/releases/download/rust-v${CODEX_VERSION}/codex-x86_64-unknown-linux-musl.tar.gz", script, StringComparison.Ordinal);
        Assert.Contains("CODEX_HOME", script, StringComparison.Ordinal);
        Assert.Contains("HERO_PASSPORT_PUBLISH_DIR", script, StringComparison.Ordinal);
        Assert.Contains(".agents/skills/hero-passport", script, StringComparison.Ordinal);
        Assert.Contains("mcp add hero-passport", script, StringComparison.Ordinal);
        Assert.Contains("mcp list", script, StringComparison.Ordinal);
        Assert.Contains("mcp get hero-passport", script, StringComparison.Ordinal);
        Assert.Contains("HeroPassport.App.dll", script, StringComparison.Ordinal);

        var workflow = File.ReadAllText(Path.Combine(repoRoot, ".github", "workflows", "ci.yml"));
        Assert.Contains("Codex host configuration smoke", workflow, StringComparison.Ordinal);
        Assert.Contains("tests/qualification/codex-host-config-smoke.sh", workflow, StringComparison.Ordinal);
        Assert.Contains("HERO_PASSPORT_PUBLISH_DIR", workflow, StringComparison.Ordinal);
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
