using Xunit;

namespace HeroPassport.AgentEvals;

public sealed class ReleaseQualificationEvidenceTests
{
    [Fact]
    public void ZeroOneReleaseMatrixRecordsPublishedExecutableEvidenceWithoutOverclaimingOtherHosts()
    {
        var path = Path.Combine(RepoRoot(), "docs", "release", "0.1.0-qualification.md");
        Assert.True(File.Exists(path), $"Missing 0.1 release evidence matrix: {path}");

        var text = File.ReadAllText(path);
        Assert.Contains("Qualification date: 2026-09-10", text, StringComparison.Ordinal);
        Assert.Contains("Release verdict: RELEASED / QUALIFIED", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Release verdict: NOT RELEASE READY", text, StringComparison.Ordinal);

        Assert.Contains("tag: v0.1.0", text, StringComparison.Ordinal);
        Assert.Contains("release id: 386030938", text, StringComparison.Ordinal);
        Assert.Contains("release state: stable, non-prerelease, immutable", text, StringComparison.Ordinal);
        Assert.Contains("source commit: 31409f10465478aba18fbd1f48f1e12a87fd61d0", text, StringComparison.Ordinal);
        Assert.Contains("post-merge main CI: CI #643 / run 34438271450 / success", text, StringComparison.Ordinal);
        Assert.Contains("release qualification: release-artifact-qualification #1 / run 34440441304 / success", text, StringComparison.Ordinal);
        Assert.Contains("publication workflow: publish-0.1.0 #1 / run 34442054952 / success", text, StringComparison.Ordinal);

        Assert.Contains("archive: hero-passport-0.1.0.zip", text, StringComparison.Ordinal);
        Assert.Contains("payload files: 139", text, StringComparison.Ordinal);
        Assert.Contains("SHA-256: 4bde06fa4e1df957b8ea88944e2f3493fd892c9bd28278e44417f95077596b80", text, StringComparison.Ordinal);
        Assert.Contains("same exact archive", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ubuntu 24.04 / ubuntu-24.04", text, StringComparison.Ordinal);
        Assert.Contains("Windows Server 2025 / windows-2025", text, StringComparison.Ordinal);
        Assert.Contains("macOS 15 / macos-15", text, StringComparison.Ordinal);

        Assert.Contains("GitHub attestation id: 46468110", text, StringComparison.Ordinal);
        Assert.Contains("Sigstore Rekor log index: 2778276913", text, StringComparison.Ordinal);
        Assert.Contains("GitHub Artifact Attestations/Sigstore", text, StringComparison.Ordinal);
        Assert.Contains("gh attestation verify", text, StringComparison.Ordinal);

        Assert.Contains("Codex | Qualified reference host | stable CLI + repo Skill + packaged MCP lifecycle/restart/replay proven", text, StringComparison.Ordinal);
        Assert.Contains("Codex CLI `0.153.4`", text, StringComparison.Ordinal);
        Assert.Contains("tool round-trip: hero.get_context", text, StringComparison.Ordinal);
        Assert.Contains("host_processes=4 replayed=true", text, StringComparison.Ordinal);

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

        Assert.Contains("framework-dependent portable .NET 10 distribution", text, StringComparison.Ordinal);
        Assert.Contains("No release blocker remains for 0.1.0", text, StringComparison.Ordinal);
        Assert.DoesNotContain("final `0.1.0` workflow has not yet been executed", text, StringComparison.Ordinal);
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
