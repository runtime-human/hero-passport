using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using HeroPassport.Infrastructure.Persistence;
using System.Diagnostics;
using Xunit;

namespace HeroPassport.Infrastructure.Tests;

public sealed class CrashRecoveryQualificationTests
{
    [Theory]
    [InlineData("before-commit", false)]
    [InlineData("after-commit", true)]
    public async Task BootstrapChildProcessKillConvergesAtCommitBoundary(string crashPhase, bool committedBeforeKill)
    {
        var token = TestContext.Current.CancellationToken;
        var directory = Path.Combine(Path.GetTempPath(), "HeroPassport.CrashQualification", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "hero-passport.db");
        var signalPath = Path.Combine(directory, "commit-boundary.signal");
        Process? child = null;

        try
        {
            await HeroPassportDatabase.InitializeAsync(databasePath, token);
            var request = new BootstrapRequest(MutationRequestId.New(), "en-US", "Crash Nova", "rpg_engineering", true, true);
            var harnessDll = CrashHarnessDll();
            Assert.True(File.Exists(harnessDll), $"Crash harness was not built at {harnessDll}.");

            var start = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            start.ArgumentList.Add(harnessDll);
            start.ArgumentList.Add("bootstrap");
            start.ArgumentList.Add(databasePath);
            start.ArgumentList.Add(crashPhase);
            start.ArgumentList.Add(request.BootstrapRequestId.ToString());
            start.ArgumentList.Add(signalPath);

            child = Process.Start(start);
            Assert.NotNull(child);
            await WaitForFileAsync(signalPath, child!, token);

            child!.Kill(entireProcessTree: true);
            await child.WaitForExitAsync(token);

            Assert.Equal(committedBeforeKill ? 1 : 0, await ScalarLongAsync(databasePath, "SELECT setup_completed FROM app_settings WHERE id=1;", token));
            Assert.Equal(committedBeforeKill ? 1 : 0, await ScalarLongAsync(databasePath, "SELECT COUNT(*) FROM heroes;", token));
            Assert.Equal(committedBeforeKill ? 1 : 0, await ScalarLongAsync(databasePath, "SELECT COUNT(*) FROM mutation_receipts WHERE operation_key='bootstrap';", token));
            Assert.Equal("ok", await ScalarStringAsync(databasePath, "PRAGMA quick_check;", token));
            Assert.Equal(0, await RowCountAsync(databasePath, "PRAGMA foreign_key_check;", token));

            var retry = await CreateApplication(databasePath).BootstrapAsync(request, token);
            Assert.Equal(committedBeforeKill, retry.Replayed);
            Assert.Equal(1, await ScalarLongAsync(databasePath, "SELECT COUNT(*) FROM heroes;", token));
            Assert.Equal(1, await ScalarLongAsync(databasePath, "SELECT COUNT(*) FROM mutation_receipts WHERE operation_key='bootstrap';", token));
        }
        finally
        {
            if (child is { HasExited: false })
            {
                child.Kill(entireProcessTree: true);
                await child.WaitForExitAsync(CancellationToken.None);
            }

            SqliteConnection.ClearAllPools();
            try { Directory.Delete(directory, recursive: true); } catch (IOException) { }
        }
    }

    private static HeroPassportApplication CreateApplication(string path) =>
        new(new SqliteHeroPassportStateStore(path), TimeProvider.System);

    private static async Task WaitForFileAsync(string path, Process child, CancellationToken token)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (File.Exists(path))
            {
                return;
            }

            if (child.HasExited)
            {
                throw new Xunit.Sdk.XunitException($"Crash harness exited before reaching the commit boundary (exit {child.ExitCode}).");
            }

            await Task.Delay(50, token);
        }

        throw new Xunit.Sdk.XunitException("Crash harness did not reach the commit boundary within 15 seconds.");
    }

    private static string CrashHarnessDll()
    {
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        return Path.Combine(RepoRoot(), "tests", "HeroPassport.CrashHarness", "bin", configuration, "net10.0", "HeroPassport.CrashHarness.dll");
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

        throw new DirectoryNotFoundException("Hero Passport repository root was not found from the infrastructure test output directory.");
    }

    private static async Task<long> ScalarLongAsync(string path, string sql, CancellationToken token)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(path, token);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync(token), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<string> ScalarStringAsync(string path, string sql, CancellationToken token)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(path, token);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(await command.ExecuteScalarAsync(token), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static async Task<int> RowCountAsync(string path, string sql, CancellationToken token)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(path, token);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(token);
        var count = 0;
        while (await reader.ReadAsync(token))
        {
            count++;
        }

        return count;
    }
}
