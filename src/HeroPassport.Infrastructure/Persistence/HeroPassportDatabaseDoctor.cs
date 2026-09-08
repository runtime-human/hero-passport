using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace HeroPassport.Infrastructure.Persistence;

public sealed record HeroPassportDatabaseDoctorReport(
    bool DatabaseExists,
    string? SqliteVersion,
    bool SqliteVersionSupported,
    string? JournalMode,
    int? Synchronous,
    bool? ForeignKeys,
    bool? TrustedSchema,
    string MigrationState,
    string? LatestAvailableMigration,
    string? LatestAppliedMigration,
    bool MigrationLockSuspected,
    bool QuickCheckPassed,
    int ForeignKeyViolationCount,
    bool Healthy);

public static class HeroPassportDatabaseDoctor
{
    private static readonly Version MinimumSupportedSqliteVersion = new(3, 53, 4);

    public static async Task<HeroPassportDatabaseDoctorReport> InspectAsync(
        string databasePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        var fullPath = Path.GetFullPath(databasePath);
        var availableMigrations = GetAvailableMigrations(fullPath);
        var latestAvailableMigration = availableMigrations.LastOrDefault();

        if (!File.Exists(fullPath))
        {
            return new HeroPassportDatabaseDoctorReport(
                DatabaseExists: false,
                SqliteVersion: null,
                SqliteVersionSupported: false,
                JournalMode: null,
                Synchronous: null,
                ForeignKeys: null,
                TrustedSchema: null,
                MigrationState: "not_initialized",
                LatestAvailableMigration: latestAvailableMigration,
                LatestAppliedMigration: null,
                MigrationLockSuspected: false,
                QuickCheckPassed: false,
                ForeignKeyViolationCount: 0,
                Healthy: false);
        }

        await using var connection = new SqliteConnection(CreateReadOnlyConnectionString(fullPath));
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ApplyDiagnosticConnectionPolicyAsync(connection, cancellationToken).ConfigureAwait(false);

        var sqliteVersion = await ReadTextScalarAsync(connection, "SELECT sqlite_version();", cancellationToken).ConfigureAwait(false);
        var sqliteVersionSupported = TryParseSqliteVersion(sqliteVersion, out var parsedVersion) &&
            parsedVersion >= MinimumSupportedSqliteVersion;
        var journalMode = await ReadTextScalarAsync(connection, "PRAGMA journal_mode;", cancellationToken).ConfigureAwait(false);
        var synchronous = await ReadIntScalarAsync(connection, "PRAGMA synchronous;", cancellationToken).ConfigureAwait(false);
        var foreignKeys = await ReadIntScalarAsync(connection, "PRAGMA foreign_keys;", cancellationToken).ConfigureAwait(false) == 1;
        var trustedSchema = await ReadIntScalarAsync(connection, "PRAGMA trusted_schema;", cancellationToken).ConfigureAwait(false) == 1;

        var historyTableExists = await TableExistsAsync(connection, "__EFMigrationsHistory", cancellationToken).ConfigureAwait(false);
        var appliedMigrations = historyTableExists
            ? await ReadAppliedMigrationsAsync(connection, cancellationToken).ConfigureAwait(false)
            : [];
        var latestAppliedMigration = appliedMigrations.LastOrDefault();
        var migrationState = DetermineMigrationState(availableMigrations, appliedMigrations, historyTableExists);

        var lockTableExists = await TableExistsAsync(connection, "__EFMigrationsLock", cancellationToken).ConfigureAwait(false);
        var migrationLockSuspected = lockTableExists &&
            await ReadLongScalarAsync(connection, "SELECT COUNT(*) FROM \"__EFMigrationsLock\";", cancellationToken).ConfigureAwait(false) > 0;

        var quickCheckPassed = await QuickCheckPassedAsync(connection, cancellationToken).ConfigureAwait(false);
        var foreignKeyViolationCount = await CountRowsAsync(connection, "PRAGMA foreign_key_check;", cancellationToken).ConfigureAwait(false);

        var healthy = sqliteVersionSupported &&
            string.Equals(journalMode, "wal", StringComparison.OrdinalIgnoreCase) &&
            synchronous == 2 &&
            foreignKeys &&
            !trustedSchema &&
            string.Equals(migrationState, "current", StringComparison.Ordinal) &&
            !migrationLockSuspected &&
            quickCheckPassed &&
            foreignKeyViolationCount == 0;

        return new HeroPassportDatabaseDoctorReport(
            DatabaseExists: true,
            SqliteVersion: sqliteVersion,
            SqliteVersionSupported: sqliteVersionSupported,
            JournalMode: journalMode,
            Synchronous: synchronous,
            ForeignKeys: foreignKeys,
            TrustedSchema: trustedSchema,
            MigrationState: migrationState,
            LatestAvailableMigration: latestAvailableMigration,
            LatestAppliedMigration: latestAppliedMigration,
            MigrationLockSuspected: migrationLockSuspected,
            QuickCheckPassed: quickCheckPassed,
            ForeignKeyViolationCount: foreignKeyViolationCount,
            Healthy: healthy);
    }

    private static string[] GetAvailableMigrations(string databasePath)
    {
        using var context = new HeroPassportDbContextFactory(databasePath).CreateDbContext();
        return context.Database.GetMigrations().ToArray();
    }

    private static string CreateReadOnlyConnectionString(string databasePath)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Default,
            ForeignKeys = true,
            Pooling = false,
            DefaultTimeout = 5,
        };
        return builder.ToString();
    }

    private static async Task ApplyDiagnosticConnectionPolicyAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA synchronous=FULL; PRAGMA trusted_schema=OFF;";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> TableExistsAsync(
        SqliteConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM sqlite_master WHERE type='table' AND name=$name);";
        command.Parameters.AddWithValue("$name", tableName);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture) == 1;
    }

    private static async Task<string[]> ReadAppliedMigrationsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var migrations = new List<string>();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT MigrationId FROM \"__EFMigrationsHistory\" ORDER BY MigrationId;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            migrations.Add(reader.GetString(0));
        }

        return [.. migrations];
    }

    private static string DetermineMigrationState(
        IReadOnlyList<string> availableMigrations,
        IReadOnlyList<string> appliedMigrations,
        bool historyTableExists)
    {
        if (!historyTableExists)
        {
            return "not_initialized";
        }

        if (availableMigrations.SequenceEqual(appliedMigrations, StringComparer.Ordinal))
        {
            return "current";
        }

        if (appliedMigrations.Count <= availableMigrations.Count &&
            appliedMigrations.SequenceEqual(availableMigrations.Take(appliedMigrations.Count), StringComparer.Ordinal))
        {
            return "pending";
        }

        return "unknown";
    }

    private static async Task<bool> QuickCheckPassedAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA quick_check;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var rowCount = 0;
        var passed = true;
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rowCount++;
            passed &= string.Equals(reader.GetString(0), "ok", StringComparison.OrdinalIgnoreCase);
        }

        return rowCount == 1 && passed;
    }

    private static async Task<int> CountRowsAsync(
        SqliteConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        var count = 0;
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            count++;
        }

        return count;
    }

    private static async Task<string> ReadTextScalarAsync(
        SqliteConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
            CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static async Task<int> ReadIntScalarAsync(
        SqliteConnection connection,
        string sql,
        CancellationToken cancellationToken) =>
        checked((int)await ReadLongScalarAsync(connection, sql, cancellationToken).ConfigureAwait(false));

    private static async Task<long> ReadLongScalarAsync(
        SqliteConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
            CultureInfo.InvariantCulture);
    }

    private static bool TryParseSqliteVersion(string value, out Version version)
    {
        var components = value.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (components.Length >= 3 &&
            int.TryParse(components[0], NumberStyles.None, CultureInfo.InvariantCulture, out var major) &&
            int.TryParse(components[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minor) &&
            int.TryParse(components[2], NumberStyles.None, CultureInfo.InvariantCulture, out var build))
        {
            version = new Version(major, minor, build);
            return true;
        }

        version = new Version(0, 0, 0);
        return false;
    }
}
