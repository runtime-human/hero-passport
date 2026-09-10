using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using HeroPassport.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using System.Diagnostics;
using System.Globalization;
using Xunit;

namespace HeroPassport.Infrastructure.Tests;

public sealed class HeroPassportMigrationCrashQualificationTests
{
    [Fact]
    public async Task KilledMigrateAsyncLeavesEfLockDoctorDiagnosesRepairRestoresMigration()
    {
        var token = TestContext.Current.CancellationToken;
        var directory = Path.Combine(
            Path.GetTempPath(),
            "HeroPassport.MigrationCrashQualification",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "hero-passport.db");
        var signalPath = Path.Combine(directory, "migration-lock.signal");
        Process? child = null;

        try
        {
            await HeroPassportDatabase.InitializeAsync(databasePath, token);
            var application = new HeroPassportApplication(
                new SqliteHeroPassportStateStore(databasePath),
                TimeProvider.System);
            var bootstrap = await application.BootstrapAsync(
                new BootstrapRequest(
                    MutationRequestId.New(),
                    "en-US",
                    "Migration Survivor",
                    "rpg_engineering",
                    true,
                    true),
                token);
            var migrationRowsBefore = await ScalarLongAsync(
                databasePath,
                "SELECT COUNT(*) FROM \"__EFMigrationsHistory\";",
                token);
            SqliteConnection.ClearAllPools();

            var harnessDll = CrashHarnessDll();
            Assert.True(File.Exists(harnessDll), $"Crash harness was not built at {harnessDll}.");

            var start = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            start.ArgumentList.Add(harnessDll);
            start.ArgumentList.Add("migration-lock");
            start.ArgumentList.Add(databasePath);
            start.ArgumentList.Add(signalPath);

            child = Process.Start(start);
            Assert.NotNull(child);
            await WaitForSignalAsync(signalPath, child!, token);

            Assert.False(child!.HasExited);
            Assert.Equal(
                1,
                await ScalarLongAsync(
                    databasePath,
                    "SELECT COUNT(*) FROM \"__EFMigrationsLock\";",
                    token));

            child.Kill(entireProcessTree: true);
            await child.WaitForExitAsync(token);

            var diagnosed = await HeroPassportDatabaseDoctor.InspectAsync(databasePath, token);
            Assert.True(diagnosed.MigrationLockSuspected);
            Assert.False(diagnosed.Healthy);
            Assert.Equal("current", diagnosed.MigrationState);
            Assert.Equal(
                migrationRowsBefore,
                await ScalarLongAsync(databasePath, "SELECT COUNT(*) FROM \"__EFMigrationsHistory\";", token));
            Assert.Equal(
                1,
                await ScalarLongAsync(
                    databasePath,
                    "SELECT COUNT(*) FROM heroes WHERE id=$heroId;",
                    token,
                    ("$heroId", bootstrap.Hero.HeroId.ToString())));

            var repair = await HeroPassportMigrationLockRepair.RepairAsync(
                databasePath,
                competingProcessesStopped: true,
                token);
            Assert.True(repair.LockCleared);
            Assert.True(repair.Before.MigrationLockSuspected);
            Assert.False(repair.After.MigrationLockSuspected);
            Assert.True(repair.After.Healthy);

            await HeroPassportDatabase.InitializeAsync(databasePath, token);
            var final = await HeroPassportDatabaseDoctor.InspectAsync(databasePath, token);

            Assert.True(final.Healthy);
            Assert.False(final.MigrationLockSuspected);
            Assert.True(final.QuickCheckPassed);
            Assert.Equal(0, final.ForeignKeyViolationCount);
            Assert.Equal("current", final.MigrationState);
            Assert.Equal(
                migrationRowsBefore,
                await ScalarLongAsync(databasePath, "SELECT COUNT(*) FROM \"__EFMigrationsHistory\";", token));
            Assert.Equal(
                1,
                await ScalarLongAsync(
                    databasePath,
                    "SELECT COUNT(*) FROM heroes WHERE id=$heroId;",
                    token,
                    ("$heroId", bootstrap.Hero.HeroId.ToString())));
        }
        finally
        {
            if (child is { HasExited: false })
            {
                child.Kill(entireProcessTree: true);
                await child.WaitForExitAsync(CancellationToken.None);
            }

            SqliteConnection.ClearAllPools();
            try { Directory.Delete(directory, recursive: true); } catch (DirectoryNotFoundException) { } catch (IOException) { }
        }
    }

    private static async Task WaitForSignalAsync(string signalPath, Process child, CancellationToken token)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (File.Exists(signalPath))
            {
                return;
            }

            if (child.HasExited)
            {
                throw new Xunit.Sdk.XunitException(
                    $"Crash harness exited before acquiring the EF migration lock (exit {child.ExitCode}).");
            }

            await Task.Delay(50, token);
        }

        throw new Xunit.Sdk.XunitException("Crash harness did not acquire the EF migration lock within 15 seconds.");
    }

    private static string CrashHarnessDll()
    {
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        return Path.Combine(
            RepoRoot(),
            "tests",
            "HeroPassport.CrashHarness",
            "bin",
            configuration,
            "net10.0",
            "HeroPassport.CrashHarness.dll");
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

        throw new DirectoryNotFoundException(
            "Hero Passport repository root was not found from the infrastructure test output directory.");
    }

    private static async Task<long> ScalarLongAsync(
        string path,
        string sql,
        CancellationToken token,
        params (string Name, object Value)[] parameters)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(path, token);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        }

        return Convert.ToInt64(
            await command.ExecuteScalarAsync(token),
            CultureInfo.InvariantCulture);
    }
}
