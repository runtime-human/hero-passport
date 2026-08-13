using HeroPassport.Infrastructure.ProjectIdentity;
using System.Diagnostics;
using Xunit;

namespace HeroPassport.Infrastructure.Tests;

public sealed class ProjectIdentityContractTests
{
    private static readonly byte[] Salt = Enumerable.Range(0, 32).Select(static value => (byte)value).ToArray();

    [Fact]
    public async Task NestedDirectoriesAndLinkedWorktreesShareDefaultIdentity()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sandbox = CreateTemporaryDirectory();
        try
        {
            var repository = Path.Combine(sandbox, "repo");
            var worktree = Path.Combine(sandbox, "feature-worktree");
            Directory.CreateDirectory(repository);
            await RunGitAsync(repository, cancellationToken, "init");
            await CommitEmptyAsync(repository, cancellationToken);
            await RunGitAsync(repository, cancellationToken, "worktree", "add", "-b", "feature", worktree);

            var nested = Path.Combine(repository, "src", "component");
            Directory.CreateDirectory(nested);

            var root = await ProjectIdentityResolver.ResolveAsync(null, repository, Salt, cancellationToken);
            var fromNested = await ProjectIdentityResolver.ResolveAsync(null, nested, Salt, cancellationToken);
            var fromWorktree = await ProjectIdentityResolver.ResolveAsync(null, worktree, Salt, cancellationToken);

            Assert.Equal("git", root.Kind);
            Assert.Equal(".", root.Scope);
            Assert.Equal(root.WorkspaceFingerprint, fromNested.WorkspaceFingerprint);
            Assert.Equal(root.WorkspaceFingerprint, fromWorktree.WorkspaceFingerprint);
        }
        finally
        {
            Directory.Delete(sandbox, recursive: true);
        }
    }

    [Fact]
    public async Task ExplicitMonorepoScopeIsStableAcrossLinkedWorktreesAndDistinctFromRepository()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sandbox = CreateTemporaryDirectory();
        try
        {
            var repository = Path.Combine(sandbox, "repo");
            var worktree = Path.Combine(sandbox, "feature-worktree");
            Directory.CreateDirectory(Path.Combine(repository, "services", "billing"));
            await RunGitAsync(repository, cancellationToken, "init");
            await CommitEmptyAsync(repository, cancellationToken);
            await RunGitAsync(repository, cancellationToken, "worktree", "add", "-b", "feature", worktree);
            Directory.CreateDirectory(Path.Combine(worktree, "services", "billing"));

            var wholeRepository = await ProjectIdentityResolver.ResolveAsync(null, repository, Salt, cancellationToken);
            var primaryScope = await ProjectIdentityResolver.ResolveAsync(
                Path.Combine(repository, "services", "billing"), repository, Salt, cancellationToken);
            var linkedScope = await ProjectIdentityResolver.ResolveAsync(
                Path.Combine(worktree, "services", "billing"), worktree, Salt, cancellationToken);

            Assert.Equal("services/billing", primaryScope.Scope);
            Assert.Equal(primaryScope.WorkspaceFingerprint, linkedScope.WorkspaceFingerprint);
            Assert.NotEqual(wholeRepository.WorkspaceFingerprint, primaryScope.WorkspaceFingerprint);
        }
        finally
        {
            Directory.Delete(sandbox, recursive: true);
        }
    }

    [Fact]
    public async Task NestedIndependentRepositoryGetsDistinctIdentity()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sandbox = CreateTemporaryDirectory();
        try
        {
            var parent = Path.Combine(sandbox, "parent");
            var nested = Path.Combine(parent, "modules", "child");
            Directory.CreateDirectory(nested);
            await RunGitAsync(parent, cancellationToken, "init");
            await RunGitAsync(nested, cancellationToken, "init");

            var parentIdentity = await ProjectIdentityResolver.ResolveAsync(null, parent, Salt, cancellationToken);
            var nestedIdentity = await ProjectIdentityResolver.ResolveAsync(null, nested, Salt, cancellationToken);

            Assert.NotEqual(parentIdentity.WorkspaceFingerprint, nestedIdentity.WorkspaceFingerprint);
        }
        finally
        {
            Directory.Delete(sandbox, recursive: true);
        }
    }

    [Fact]
    public async Task BareRepositoryIsRejectedWithoutLeakingItsPath()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sandbox = CreateTemporaryDirectory();
        try
        {
            var bare = Path.Combine(sandbox, "private-bare-repository.git");
            Directory.CreateDirectory(bare);
            await RunGitAsync(bare, cancellationToken, "init", "--bare");

            var exception = await Assert.ThrowsAsync<ProjectIdentityException>(() =>
                ProjectIdentityResolver.ResolveAsync(bare, bare, Salt, cancellationToken));

            Assert.Equal("HP313", exception.Code);
            Assert.DoesNotContain(bare, exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(sandbox, recursive: true);
        }
    }

    [Fact]
    public async Task StandaloneFinalDirectorySymlinkResolvesToTargetIdentity()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var cancellationToken = TestContext.Current.CancellationToken;
        var sandbox = CreateTemporaryDirectory();
        try
        {
            var target = Path.Combine(sandbox, "target");
            var link = Path.Combine(sandbox, "link");
            Directory.CreateDirectory(target);
            Directory.CreateSymbolicLink(link, target);

            var targetIdentity = await ProjectIdentityResolver.ResolveAsync(null, target, Salt, cancellationToken);
            var linkIdentity = await ProjectIdentityResolver.ResolveAsync(null, link, Salt, cancellationToken);

            Assert.Equal(targetIdentity.WorkspaceFingerprint, linkIdentity.WorkspaceFingerprint);
        }
        finally
        {
            Directory.Delete(sandbox, recursive: true);
        }
    }

    [Fact]
    public async Task GitPathsWithSpacesUnicodeAndLeadingDashComponentsAreLiteralArguments()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sandbox = CreateTemporaryDirectory();
        try
        {
            var repository = Path.Combine(sandbox, "repo Ω with spaces");
            var nested = Path.Combine(repository, "-component", "δ");
            Directory.CreateDirectory(nested);
            await RunGitAsync(repository, cancellationToken, "init");

            var root = await ProjectIdentityResolver.ResolveAsync(null, repository, Salt, cancellationToken);
            var nestedIdentity = await ProjectIdentityResolver.ResolveAsync(null, nested, Salt, cancellationToken);

            Assert.Equal(root.WorkspaceFingerprint, nestedIdentity.WorkspaceFingerprint);
        }
        finally
        {
            Directory.Delete(sandbox, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "hero-passport-project-identity-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static Task CommitEmptyAsync(string repository, CancellationToken cancellationToken) =>
        RunGitAsync(
            repository,
            cancellationToken,
            "-c", "user.name=Hero Passport",
            "-c", "user.email=hero@example.invalid",
            "commit", "--allow-empty", "-m", "initial");

    private static async Task RunGitAsync(string workingDirectory, CancellationToken cancellationToken, params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        Assert.True(process.Start());
        var standardOutput = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        Assert.True(process.ExitCode == 0, $"git {string.Join(' ', arguments)} failed. stdout={standardOutput} stderr={standardError}");
    }
}
