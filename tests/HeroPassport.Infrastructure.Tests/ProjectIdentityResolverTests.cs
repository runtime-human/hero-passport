using System.Diagnostics;
using HeroPassport.Infrastructure.ProjectIdentity;
using Xunit;

namespace HeroPassport.Infrastructure.Tests;

public sealed class ProjectIdentityResolverTests
{
    [Fact]
    public async Task NestedWorkingDirectoriesShareDefaultGitIdentity()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var root = CreateTemporaryDirectory();
        try
        {
            var repository = Path.Combine(root, "repo");
            Directory.CreateDirectory(repository);
            await RunGitAsync(repository, cancellationToken, "init");

            var nested = Path.Combine(repository, "src", "component");
            Directory.CreateDirectory(nested);

            var resolver = new ProjectIdentityResolver();
            var salt = Enumerable.Range(0, 32).Select(static value => (byte)value).ToArray();

            var fromRoot = await resolver.ResolveAsync(null, repository, salt, cancellationToken);
            var fromNested = await resolver.ResolveAsync(null, nested, salt, cancellationToken);
            var explicitNested = await resolver.ResolveAsync(nested, repository, salt, cancellationToken);

            Assert.Equal("git", fromRoot.Kind);
            Assert.Equal(".", fromRoot.Scope);
            Assert.Equal(fromRoot.WorkspaceFingerprint, fromNested.WorkspaceFingerprint);
            Assert.Equal("src/component", explicitNested.Scope);
            Assert.NotEqual(fromRoot.WorkspaceFingerprint, explicitNested.WorkspaceFingerprint);
            Assert.Matches("^[0-9a-f]{64}$", fromRoot.WorkspaceFingerprint);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task LinkedWorktreesShareGitIdentity()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var root = CreateTemporaryDirectory();
        try
        {
            var repository = Path.Combine(root, "repo");
            var worktree = Path.Combine(root, "feature-worktree");
            Directory.CreateDirectory(repository);
            await RunGitAsync(repository, cancellationToken, "init");
            await RunGitAsync(repository, cancellationToken, "-c", "user.name=Hero Passport", "-c", "user.email=hero@example.invalid", "commit", "--allow-empty", "-m", "initial");
            await RunGitAsync(repository, cancellationToken, "worktree", "add", "-b", "feature", worktree);

            var resolver = new ProjectIdentityResolver();
            var salt = Enumerable.Repeat((byte)0x5A, 32).ToArray();

            var primary = await resolver.ResolveAsync(null, repository, salt, cancellationToken);
            var linked = await resolver.ResolveAsync(null, worktree, salt, cancellationToken);

            Assert.Equal(primary.WorkspaceFingerprint, linked.WorkspaceFingerprint);
            Assert.Equal(".", linked.Scope);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task NonGitDirectoryUsesStandaloneIdentity()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var root = CreateTemporaryDirectory();
        try
        {
            var resolver = new ProjectIdentityResolver();
            var salt = Enumerable.Repeat((byte)0xA5, 32).ToArray();

            var identity = await resolver.ResolveAsync(null, root, salt, cancellationToken);

            Assert.Equal("standalone", identity.Kind);
            Assert.Equal(".", identity.Scope);
            Assert.Matches("^[0-9a-f]{64}$", identity.WorkspaceFingerprint);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "hero-passport-project-identity-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

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
