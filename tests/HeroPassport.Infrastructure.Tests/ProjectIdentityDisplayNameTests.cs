using HeroPassport.Infrastructure.ProjectIdentity;
using System.Diagnostics;
using Xunit;

namespace HeroPassport.Infrastructure.Tests;

public sealed class ProjectIdentityDisplayNameTests
{
    [Fact]
    public async Task ExplicitGitScopeUsesScopeLeafAsDisplayName()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sandbox = Path.Combine(Path.GetTempPath(), "hero-passport-project-name-tests", Guid.NewGuid().ToString("N"));
        var repository = Path.Combine(sandbox, "monorepo");
        var scope = Path.Combine(repository, "services", "billing");
        Directory.CreateDirectory(scope);

        try
        {
            await RunGitAsync(repository, cancellationToken, "init");
            var salt = Enumerable.Range(0, 32).Select(static value => (byte)value).ToArray();

            var identity = await ProjectIdentityResolver.ResolveAsync(scope, repository, salt, cancellationToken);

            Assert.Equal("services/billing", identity.Scope);
            Assert.Equal("billing", identity.DisplayName);
        }
        finally
        {
            Directory.Delete(sandbox, recursive: true);
        }
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
        Assert.True(process.ExitCode == 0, $"git failed. stdout={standardOutput} stderr={standardError}");
    }
}
