using Microsoft.Data.Sqlite;
using System.Data;

namespace HeroPassport.Infrastructure.Persistence;

public sealed record HeroPassportMigrationLockRepairResult(
    bool LockCleared,
    HeroPassportDatabaseDoctorReport Before,
    HeroPassportDatabaseDoctorReport After);

public static class HeroPassportMigrationLockRepair
{
    public static async Task<HeroPassportMigrationLockRepairResult> RepairAsync(
        string databasePath,
        bool competingProcessesStopped,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        if (!competingProcessesStopped)
        {
            throw new ArgumentException(
                "Migration-lock repair requires explicit confirmation that competing Hero Passport processes are stopped.",
                nameof(competingProcessesStopped));
        }

        var fullPath = Path.GetFullPath(databasePath);
        var before = await HeroPassportDatabaseDoctor
            .InspectAsync(fullPath, cancellationToken)
            .ConfigureAwait(false);

        EnsureRepairPreconditions(before);
        if (!before.MigrationLockSuspected)
        {
            return new HeroPassportMigrationLockRepairResult(
                LockCleared: false,
                Before: before,
                After: before);
        }

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWrite,
            Cache = SqliteCacheMode.Default,
            ForeignKeys = true,
            Pooling = false,
            DefaultTimeout = 5,
        };

        await using (var connection = new SqliteConnection(builder.ToString()))
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await SqliteConnectionPolicy.ApplyAsync(connection, cancellationToken).ConfigureAwait(false);

            await using var transaction = connection.BeginTransaction(
                IsolationLevel.Serializable,
                deferred: false);
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM \"__EFMigrationsLock\";";
            var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            if (affectedRows < 1)
            {
                throw new InvalidOperationException(
                    "Migration lock changed during explicit repair; no lock row was cleared.");
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        var after = await HeroPassportDatabaseDoctor
            .InspectAsync(fullPath, cancellationToken)
            .ConfigureAwait(false);
        EnsurePostRepairState(before, after);

        return new HeroPassportMigrationLockRepairResult(
            LockCleared: true,
            Before: before,
            After: after);
    }

    private static void EnsureRepairPreconditions(HeroPassportDatabaseDoctorReport report)
    {
        if (!report.DatabaseExists)
        {
            throw new InvalidOperationException("Hero Passport database is not initialized.");
        }

        var migrationStateRepairable =
            string.Equals(report.MigrationState, "current", StringComparison.Ordinal) ||
            string.Equals(report.MigrationState, "pending", StringComparison.Ordinal);

        if (!report.SqliteVersionSupported ||
            !string.Equals(report.JournalMode, "wal", StringComparison.OrdinalIgnoreCase) ||
            report.Synchronous != 2 ||
            report.ForeignKeys != true ||
            report.TrustedSchema != false ||
            !migrationStateRepairable ||
            !report.QuickCheckPassed ||
            report.ForeignKeyViolationCount != 0)
        {
            throw new InvalidOperationException(
                "Migration-lock repair refused because database policy, migration state, or integrity checks are unsafe.");
        }
    }

    private static void EnsurePostRepairState(
        HeroPassportDatabaseDoctorReport before,
        HeroPassportDatabaseDoctorReport after)
    {
        if (after.MigrationLockSuspected ||
            !after.QuickCheckPassed ||
            after.ForeignKeyViolationCount != 0 ||
            !string.Equals(before.MigrationState, after.MigrationState, StringComparison.Ordinal) ||
            before.LatestAppliedMigration != after.LatestAppliedMigration)
        {
            throw new InvalidOperationException(
                "Migration-lock repair completed but post-repair migration or integrity revalidation failed.");
        }
    }
}
