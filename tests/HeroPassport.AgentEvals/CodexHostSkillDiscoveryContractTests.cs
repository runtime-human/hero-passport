using Xunit;

namespace HeroPassport.AgentEvals;

public sealed class CodexHostSkillDiscoveryContractTests
{
    [Fact]
    public void RuntimeSmokeProvesPackagedHeroPassportSkillIsModelVisible()
    {
        var path = Path.Combine(RepoRoot(), "tests", "qualification", "codex-host-runtime-smoke.py");
        Assert.True(File.Exists(path), $"Missing Codex runtime smoke harness: {path}");

        var text = File.ReadAllText(path);
        Assert.Contains("developer_texts", text, StringComparison.Ordinal);
        Assert.Contains("### Available skills", text, StringComparison.Ordinal);
        Assert.Contains("- hero-passport:", text, StringComparison.Ordinal);
        Assert.Contains(".agents/skills/hero-passport/SKILL.md", text, StringComparison.Ordinal);
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
