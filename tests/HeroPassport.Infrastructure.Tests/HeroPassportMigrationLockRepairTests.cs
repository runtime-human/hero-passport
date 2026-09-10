using HeroPassport.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Xunit;

namespace HeroPassport.Infrastructure.Tests;

public sealed class HeroPassportMigrationLockRepairTests
{
    [Fact]
    public async Task RepairRequiresExplicitStoppedProcessesConfirmationAndNeverClearsImplicitly()
    {
        var token = TestContext.Current.CancellationToken;
        var databasePath = CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(databasePath, token);
            await SeedMigrationLockAsync(databasePath, token);

            var before = await HeroPassportDatabaseDoctor.InspectAsync(databasePath, token);
            Assert.True(before.MigrationLockSuspected);

            await Assert.ThrowsAsync<ArgumentException>(() =>
                HeroPassportMigrationLockRepair.RepairAsync(
                    databasePath,
                    competingProcessesStopped: false,
                    token));

            var afterRejectedRepair = await HeroPassportDatabaseDoctor.InspectAsync(databasePath, token);
            Assert.True(afterRejectedRepair.MigrationLockSuspected);
            Assert.Equal(1L, await CountMigrationLockRowsAsync(databasePath, token));
        }
        finally
        {
            DeleteDatabaseDirectory(databasePath);
        }
    }

    [Fact]
    public async Task ExplicitRepairClearsOnlyLockRowsThenRevalidatesMigrationAndIntegrity()
    {
        var token = TestContext.Current.CancellationToken;
        var databasePath = CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(databasePath, token);
            await SeedMigrationLockAsync(databasePath, token);

            var result = await HeroPassportMigrationLockRepair.RepairAsync(
                databasePath,
                competingProcessesStopped: true,
                token);

            Assert.True(result.LockCleared);
            Assert.True(result.Before.MigrationLockSuspected);
            Assert.False(result.After.MigrationLockSuspected);
            Assert.Equal("current", result.After.MigrationState);
            Assert.True(result.After.QuickCheckPassed);
            Assert.Equal(0, result.After.ForeignKeyViolationCount);
            Assert.True(result.After.Healthy);
            Assert.Equal(0L, await CountMigrationLockRowsAsync(databasePath, token));

            await HeroPassportDatabase.InitializeAsync(databasePath, token);
            var afterMigration = await HeroPassportDatabaseDoctor.InspectAsync(databasePath, token);
            Assert.True(afterMigration.Healthy);
        }
        finally
        {
            DeleteDatabaseDirectory(databasePath);
        }
    }

    [Fact]
    public void RepairSafetyPolicyRejectsUnsupportedStorageEvenWhenDatabaseChecksOtherwisePass()
    {
        var unsupported = RepairableReport(storageLocationSupported: false);
        var supported = RepairableReport(storageLocationSupported: true);

        Assert.False(HeroPassportMigrationLockRepair.IsRepairSafe(unsupported));
        Assert.True(HeroPassportMigrationLockRepair.IsRepairSafe(supported));
    }

    [Fact]
    public async Task MissingDatabaseRepairDoesNotCreateStorage()
    {
        var token = TestContext.Current.CancellationToken;
        var root = Path.Combine(Path.GetTempPath(), "HeroPassport.MigrationLockRepair.Tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(root, "nested", "hero-passport.db");
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                HeroPassportMigrationLockRepair.RepairAsync(
                    databasePath,
                    competingProcessesStopped: true,
                    token));

            Assert.False(File.Exists(databasePath));
            Assert.False(Directory.Exists(Path.GetDirectoryName(databasePath)!));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static HeroPassportDatabaseDoctorReport RepairableReport(bool storageLocationSupported) =>
        new(
            DatabaseExists: true,
            StorageLocationSupported: storageLocationSupported,
            StorageLocationKind: storageLocationSupported ? "local" : "network",
            StorageDriveType: storageLocationSupported ? "fixed" : "network",
            SqliteVersion: "3.53.4",
            SqliteVersionSupported: true,
            JournalMode: "wal",
            Synchronous: 2,
            ForeignKeys: true,
            TrustedSchema: false,
            MigrationState: "current",
            LatestAvailableMigration: "migration",
            LatestAppliedMigration: "migration",
            MigrationLockSuspected: true,
            QuickCheckPassed: true,
            ForeignKeyViolationCount: 0,
            Healthy: false);

    private static async Task SeedMigrationLockAsync(string databasePath, CancellationToken token)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(databasePath, token);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS "__EFMigrationsLock" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK___EFMigrationsLock" PRIMARY KEY,
                "Timestamp" TEXT NOT NULL
            );
            DELETE FROM "__EFMigrationsLock";
            INSERT INTO "__EFMigrationsLock" ("Id", "Timestamp") VALUES (1, '2026-09-08 00:00:00+00:00');
            """;
        await command.ExecuteNonQueryAsync(token);
    }

    private static async Task<long> CountMigrationLockRowsAsync(string databasePath, CancellationToken token)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
            ForeignKeys = true,
        };
        await using var connection = new SqliteConnection(builder.ToString());
        await connection.OpenAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM \"__EFMigrationsLock\";";
        return Convert.ToInt64(await command.ExecuteScalarAsync(token), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string CreateDatabasePath()
    {
        var root = Path.Combine(Path.GetTempPath(), "HeroPassport.MigrationLockRepair.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return Path.Combine(root, "hero-passport.db");
    }

    private static void DeleteDatabaseDirectory(string databasePath)
    {
        SqliteConnection.ClearAllPools();
        var directory = Path.GetDirectoryName(databasePath)!;
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
