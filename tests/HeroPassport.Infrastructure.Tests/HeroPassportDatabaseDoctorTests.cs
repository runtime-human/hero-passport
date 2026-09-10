using HeroPassport.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Xunit;

namespace HeroPassport.Infrastructure.Tests;

public sealed class HeroPassportDatabaseDoctorTests
{
    private static readonly string[] LocalDriveTypes = ["fixed", "removable", "ram"];

    [Fact]
    public async Task MissingDatabaseInspectionDoesNotCreateStorage()
    {
        var token = TestContext.Current.CancellationToken;
        var root = Path.Combine(Path.GetTempPath(), "HeroPassport.Doctor.Tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "nested", "hero-passport.db");
        try
        {
            var report = await HeroPassportDatabaseDoctor.InspectAsync(path, token);

            Assert.False(report.DatabaseExists);
            Assert.False(report.Healthy);
            Assert.Equal("not_initialized", report.MigrationState);
            Assert.Null(report.SqliteVersion);
            Assert.True(report.StorageLocationSupported);
            Assert.Equal("local", report.StorageLocationKind);
            Assert.False(string.IsNullOrWhiteSpace(report.StorageDriveType));
            Assert.False(File.Exists(path));
            Assert.False(Directory.Exists(root));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch (DirectoryNotFoundException) { } catch (IOException) { }
        }
    }

    [Fact]
    public async Task InitializedDatabaseReportsQualifiedPolicyMigrationsAndIntegrityWithoutMutation()
    {
        var token = TestContext.Current.CancellationToken;
        var path = CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            var before = await SnapshotAsync(path, token);

            var report = await HeroPassportDatabaseDoctor.InspectAsync(path, token);

            Assert.True(report.DatabaseExists);
            Assert.True(report.SqliteVersionSupported);
            Assert.NotNull(report.SqliteVersion);
            Assert.Equal("wal", report.JournalMode);
            Assert.Equal(2, report.Synchronous);
            Assert.True(report.ForeignKeys);
            Assert.False(report.TrustedSchema);
            Assert.Equal("current", report.MigrationState);
            Assert.False(string.IsNullOrWhiteSpace(report.LatestAvailableMigration));
            Assert.Equal(report.LatestAvailableMigration, report.LatestAppliedMigration);
            Assert.False(report.MigrationLockSuspected);
            Assert.True(report.QuickCheckPassed);
            Assert.Equal(0, report.ForeignKeyViolationCount);
            Assert.True(report.StorageLocationSupported);
            Assert.Equal("local", report.StorageLocationKind);
            Assert.Contains(report.StorageDriveType, LocalDriveTypes);
            Assert.True(report.Healthy);

            var after = await SnapshotAsync(path, token);
            Assert.Equal(before, after);
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task MigrationLockIsDiagnosedButNeverClearedByDoctor()
    {
        var token = TestContext.Current.CancellationToken;
        var path = CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            await using (var connection = await HeroPassportDatabase.OpenConnectionAsync(path, token))
            await using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    """
                    CREATE TABLE IF NOT EXISTS "__EFMigrationsLock" (
                        "Id" INTEGER NOT NULL CONSTRAINT "PK___EFMigrationsLock" PRIMARY KEY,
                        "Timestamp" TEXT NOT NULL
                    );
                    INSERT OR REPLACE INTO "__EFMigrationsLock"("Id", "Timestamp")
                    VALUES (1, '2026-09-08T00:00:00.0000000+00:00');
                    """;
                await command.ExecuteNonQueryAsync(token);
            }

            var report = await HeroPassportDatabaseDoctor.InspectAsync(path, token);

            Assert.True(report.MigrationLockSuspected);
            Assert.False(report.Healthy);

            await using var verifyConnection = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
            await verifyConnection.OpenAsync(token);
            await using var verifyCommand = verifyConnection.CreateCommand();
            verifyCommand.CommandText = "SELECT COUNT(*) FROM \"__EFMigrationsLock\";";
            Assert.Equal(1L, Convert.ToInt64(await verifyCommand.ExecuteScalarAsync(token), System.Globalization.CultureInfo.InvariantCulture));
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Theory]
    [InlineData(DriveType.Fixed, true, "local")]
    [InlineData(DriveType.Removable, true, "local")]
    [InlineData(DriveType.Ram, true, "local")]
    [InlineData(DriveType.Network, false, "network")]
    [InlineData(DriveType.Unknown, false, "unknown")]
    [InlineData(DriveType.NoRootDirectory, false, "unknown")]
    [InlineData(DriveType.CDRom, false, "unsupported")]
    public void StorageLocationPolicyFailsClosedForNonLocalOrUnknownDrives(
        DriveType driveType,
        bool expectedSupported,
        string expectedKind)
    {
        var evaluation = HeroPassportStorageLocationPolicy.Classify(driveType, isUnc: false);

        Assert.Equal(expectedSupported, evaluation.Supported);
        Assert.Equal(expectedKind, evaluation.Kind);
        Assert.Equal(driveType.ToString().ToLowerInvariant(), evaluation.DriveType);
    }

    [Fact]
    public void StorageLocationPolicyTreatsWindowsUncAsNetworkBeforeDriveInspection()
    {
        var evaluation = HeroPassportStorageLocationPolicy.Classify(DriveType.Fixed, isUnc: true);

        Assert.False(evaluation.Supported);
        Assert.Equal("network", evaluation.Kind);
        Assert.Equal("network", evaluation.DriveType);
    }

    private static async Task<DatabaseSnapshot> SnapshotAsync(string path, CancellationToken token)
    {
        await using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
        await connection.OpenAsync(token);
        return new DatabaseSnapshot(
            await ScalarAsync(connection, "SELECT COUNT(*) FROM app_settings;", token),
            await ScalarAsync(connection, "SELECT COUNT(*) FROM __EFMigrationsHistory;", token),
            await ScalarAsync(connection, "SELECT COUNT(*) FROM heroes;", token));
    }

    private static async Task<long> ScalarAsync(SqliteConnection connection, string sql, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync(token), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string CreateDatabasePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "HeroPassport.Doctor.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "hero-passport.db");
    }

    private static void DeleteDatabase(string path)
    {
        SqliteConnection.ClearAllPools();
        var directory = Path.GetDirectoryName(path);
        if (directory is null)
        {
            return;
        }

        try { Directory.Delete(directory, recursive: true); } catch (DirectoryNotFoundException) { } catch (IOException) { }
    }

    private sealed record DatabaseSnapshot(long SettingsRows, long MigrationRows, long HeroRows);
}
