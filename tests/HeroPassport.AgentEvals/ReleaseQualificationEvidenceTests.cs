using Xunit;

namespace HeroPassport.AgentEvals;

public sealed class ReleaseQualificationEvidenceTests
{
    [Fact]
    public void ZeroOneReleaseMatrixRecordsEvidenceWithoutOverclaimingHostQualification()
    {
        var path = Path.Combine(RepoRoot(), "docs", "release", "0.1.0-qualification.md");
        Assert.True(File.Exists(path), $"Missing Task 17 release evidence matrix: {path}");

        var text = File.ReadAllText(path);
        Assert.Contains("Qualification date: 2026-09-09", text, StringComparison.Ordinal);
        Assert.Contains("Release verdict: NOT RELEASE READY", text, StringComparison.Ordinal);
        Assert.Contains("Codex | reference host | pending host CLI smoke", text, StringComparison.Ordinal);
        Assert.Contains("Claude Code | documented compatible | host smoke pending", text, StringComparison.Ordinal);
        Assert.Contains("VS Code | documented compatible | host smoke pending", text, StringComparison.Ordinal);
        Assert.Contains("Cursor | documented compatible | host smoke pending", text, StringComparison.Ordinal);
        Assert.Contains("Zed | documented compatible | host smoke pending", text, StringComparison.Ordinal);
        Assert.Contains("JetBrains | MCP/Core candidate | ambient Skill not qualified", text, StringComparison.Ordinal);
        Assert.Contains("ChatGPT | not release-qualified | local stdio 0.1 path not proven", text, StringComparison.Ordinal);

        Assert.DoesNotContain("Claude Code | Qualified", text, StringComparison.Ordinal);
        Assert.DoesNotContain("VS Code | Qualified", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Cursor | Qualified", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Zed | Qualified", text, StringComparison.Ordinal);
        Assert.DoesNotContain("JetBrains | Qualified", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ChatGPT | Qualified", text, StringComparison.Ordinal);
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
