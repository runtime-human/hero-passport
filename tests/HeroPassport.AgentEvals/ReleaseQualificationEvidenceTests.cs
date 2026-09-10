using Xunit;

namespace HeroPassport.AgentEvals;

public sealed class ReleaseQualificationEvidenceTests
{
    [Fact]
    public void ZeroOneReleaseMatrixRecordsCurrentExecutableEvidenceWithoutOverclaimingOtherHosts()
    {
        var path = Path.Combine(RepoRoot(), "docs", "release", "0.1.0-qualification.md");
        Assert.True(File.Exists(path), $"Missing Task 17 release evidence matrix: {path}");

        var text = File.ReadAllText(path);
        Assert.Contains("Qualification date: 2026-09-10", text, StringComparison.Ordinal);
        Assert.Contains("Release verdict: NOT RELEASE READY", text, StringComparison.Ordinal);

        Assert.Contains("head: 509f2389c10d11966772b8e7058a90bd48943d47", text, StringComparison.Ordinal);
        Assert.Contains("Linux full CI: CI #628 / run 34435792659 / success", text, StringComparison.Ordinal);
        Assert.Contains("Cross-platform qualification: release-platform #5 / run 34435792658 / success", text, StringComparison.Ordinal);
        Assert.Contains("Ubuntu 24.04.5 LTS", text, StringComparison.Ordinal);
        Assert.Contains("Microsoft Windows Server 2025 10.0.26100", text, StringComparison.Ordinal);
        Assert.Contains("macOS 15.7.9", text, StringComparison.Ordinal);
        Assert.Contains("Infrastructure 45/45 GREEN", text, StringComparison.Ordinal);
        Assert.Contains("PackagedE2E 3/3 GREEN", text, StringComparison.Ordinal);

        Assert.Contains("Codex CLI: 0.153.4", text, StringComparison.Ordinal);
        Assert.Contains("tool round-trip: hero.get_context", text, StringComparison.Ordinal);
        Assert.Contains("host_processes=4 replayed=true", text, StringComparison.Ordinal);
        Assert.Contains(
            "Codex | Qualified reference host | stable CLI + repo Skill + packaged MCP lifecycle/restart/replay proven",
            text,
            StringComparison.Ordinal);

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

        Assert.Contains("framework-dependent portable ZIP", text, StringComparison.Ordinal);
        Assert.Contains("single cross-platform release archive", text, StringComparison.Ordinal);
        Assert.Contains("GitHub Artifact Attestations/Sigstore", text, StringComparison.Ordinal);
        Assert.Contains("exact archive", text, StringComparison.Ordinal);
        Assert.Contains("final `0.1.0` workflow has not yet been executed from the frozen `main` release commit", text, StringComparison.Ordinal);
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
