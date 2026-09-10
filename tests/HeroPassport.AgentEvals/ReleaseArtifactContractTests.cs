using Xunit;

namespace HeroPassport.AgentEvals;

public sealed class ReleaseArtifactContractTests
{
    private const string AttestActionCommit = "1e69f48acb82d1966a394da916b4c1698aa569d6";
    private const string DownloadArtifactActionCommit = "3e5f45b2cfb9172054b4087a40e8e0b5a5461e7c";

    [Fact]
    public void ReleaseArtifactUsesSingleVersionAuthorityDeterministicArchiveAndPinnedProvenance()
    {
        var root = RepoRoot();

        var props = File.ReadAllText(Path.Combine(root, "Directory.Build.props"));
        Assert.Contains("<VersionPrefix>0.1.0</VersionPrefix>", props, StringComparison.Ordinal);
        Assert.Contains("<VersionSuffix", props, StringComparison.Ordinal);
        Assert.Contains(">dev</VersionSuffix>", props, StringComparison.Ordinal);

        var versions = File.ReadAllText(Path.Combine(root, "src", "HeroPassport.Application", "Runtime", "HeroPassportVersions.cs"));
        Assert.DoesNotContain("const string ProductVersion = \"0.1.0-dev\"", versions, StringComparison.Ordinal);
        Assert.Contains("AssemblyInformationalVersionAttribute", versions, StringComparison.Ordinal);

        var packagerPath = Path.Combine(root, "tests", "qualification", "package-release.py");
        Assert.True(File.Exists(packagerPath), $"Missing deterministic release packager: {packagerPath}");
        var packager = File.ReadAllText(packagerPath);
        Assert.Contains("hero-passport-", packager, StringComparison.Ordinal);
        Assert.Contains("sha256", packager, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ZipInfo", packager, StringComparison.Ordinal);
        Assert.Contains("1980, 1, 1, 0, 0, 0", packager, StringComparison.Ordinal);
        Assert.Contains("sorted(", packager, StringComparison.Ordinal);

        var workflowPath = Path.Combine(root, ".github", "workflows", "release.yml");
        Assert.True(File.Exists(workflowPath), $"Missing release workflow: {workflowPath}");
        var workflow = File.ReadAllText(workflowPath);
        Assert.Contains("workflow_dispatch:", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("pull_request:", workflow, StringComparison.Ordinal);
        Assert.Contains("expected_sha:", workflow, StringComparison.Ordinal);
        Assert.Contains("test \"$GITHUB_SHA\" = \"$EXPECTED_SHA\"", workflow, StringComparison.Ordinal);
        Assert.Contains("-p:UseAppHost=false", workflow, StringComparison.Ordinal);
        Assert.Contains("id-token: write", workflow, StringComparison.Ordinal);
        Assert.Contains("attestations: write", workflow, StringComparison.Ordinal);
        Assert.Contains("build-release-candidate:", workflow, StringComparison.Ordinal);
        Assert.Contains("qualify-exact-archive:", workflow, StringComparison.Ordinal);
        Assert.Contains("provenance:", workflow, StringComparison.Ordinal);
        Assert.Contains("ubuntu-24.04", workflow, StringComparison.Ordinal);
        Assert.Contains("windows-2025", workflow, StringComparison.Ordinal);
        Assert.Contains("macos-15", workflow, StringComparison.Ordinal);
        Assert.Contains($"actions/download-artifact@{DownloadArtifactActionCommit}", workflow, StringComparison.Ordinal);
        Assert.Contains("needs: build-release-candidate", workflow, StringComparison.Ordinal);
        Assert.Contains("needs: qualify-exact-archive", workflow, StringComparison.Ordinal);
        Assert.Contains($"actions/attest@{AttestActionCommit}", workflow, StringComparison.Ordinal);
        Assert.Contains("subject-path:", workflow, StringComparison.Ordinal);
        Assert.Contains("package-release.py", workflow, StringComparison.Ordinal);
        Assert.Contains("SHA256SUMS", workflow, StringComparison.Ordinal);
        Assert.Contains("HeroPassport.PackagedE2E", workflow, StringComparison.Ordinal);
        Assert.Contains("matrix.os == 'ubuntu-24.04'", workflow, StringComparison.Ordinal);
        Assert.Contains("gh attestation verify", workflow, StringComparison.Ordinal);

        var distribution = File.ReadAllText(Path.Combine(root, "docs", "DISTRIBUTION.md"));
        Assert.Contains("framework-dependent portable ZIP", distribution, StringComparison.Ordinal);
        Assert.Contains("single cross-platform release archive", distribution, StringComparison.Ordinal);
        Assert.Contains("dotnet HeroPassport.App.dll", distribution, StringComparison.Ordinal);
        Assert.Contains("compatible .NET 10 runtime", distribution, StringComparison.Ordinal);
        Assert.Contains("not a native apphost", distribution, StringComparison.Ordinal);
        Assert.Contains("same exact archive", distribution, StringComparison.Ordinal);

        var decisionLog = File.ReadAllText(Path.Combine(root, "docs", "DECISION-LOG.md"));
        Assert.Contains("ADR-073", decisionLog, StringComparison.Ordinal);
        Assert.Contains("framework-dependent portable ZIP", decisionLog, StringComparison.Ordinal);
        Assert.Contains("same exact archive", decisionLog, StringComparison.Ordinal);
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
