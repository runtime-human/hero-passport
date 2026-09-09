using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace HeroPassport.Infrastructure.Persistence;

public sealed record HeroPassportDatabaseBackupResult(
    bool Validated,
    string DestinationPath,
    long SizeBytes,
    string MigrationState);

public static class HeroPassportDatabaseBackup
{
    public static async Task<HeroPassportDatabaseBackupResult> CreateAsync(
        string sourceDatabasePath,
        string destinationDatabasePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDatabasePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDatabasePath);

        var sourcePath = Path.GetFullPath(sourceDatabasePath);
        var destinationPath = Path.GetFullPath(destinationDatabasePath);
        if (string.Equals(sourcePath, destinationPath, PathComparison))
        {
            throw new IOException("Backup destination must differ from the active Hero Passport database.");
        }

        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("Hero Passport database was not found.", sourcePath);
        }

        if (File.Exists(destinationPath))
        {
            throw new IOException("Backup destination already exists and will not be overwritten.");
        }

        var sourceReport = await HeroPassportDatabaseDoctor
            .InspectAsync(sourcePath, cancellationToken)
            .ConfigureAwait(false);
        if (!sourceReport.Healthy)
        {
            throw new InvalidOperationException(
                "Backup refused because the active Hero Passport database did not pass doctor validation.");
        }

        var destinationDirectory = Path.GetDirectoryName(destinationPath)
            ?? throw new InvalidOperationException("Backup destination directory could not be resolved.");
        Directory.CreateDirectory(destinationDirectory);

        var temporaryPath = Path.Combine(
            destinationDirectory,
            $".{Path.GetFileName(destinationPath)}.{Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)}.tmp");

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await BackupToTemporaryAsync(sourcePath, temporaryPath, cancellationToken).ConfigureAwait(false);

            var validation = await ValidateCandidateAsync(temporaryPath, cancellationToken).ConfigureAwait(false);
            if (!validation.Validated)
            {
                throw new InvalidDataException(
                    "Backup candidate failed integrity, migration, or schema metadata validation.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, destinationPath, overwrite: false);
            var sizeBytes = new FileInfo(destinationPath).Length;
            return new HeroPassportDatabaseBackupResult(
                Validated: true,
                DestinationPath: destinationPath,
                SizeBytes: sizeBytes,
                MigrationState: validation.MigrationState);
        }
        catch
        {
            TryDeleteUnpublishedCandidate(temporaryPath);
            throw;
        }
    }

    private static async Task BackupToTemporaryAsync(
        string sourcePath,
        string temporaryPath,
        CancellationToken cancellationToken)
    {
        await using var source = new SqliteConnection(CreateConnectionString(sourcePath, SqliteOpenMode.ReadOnly));
        await using var destination = new SqliteConnection(CreateConnectionString(temporaryPath, SqliteOpenMode.ReadWriteCreate));
        await source.OpenAsync(cancellationToken).ConfigureAwait(false);
        await destination.OpenAsync(cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        source.BackupDatabase(destination);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static async Task<BackupValidationReport> ValidateCandidateAsync(
        string candidatePath,
        CancellationToken cancellationToken)
    {
        var availableMigrations = GetAvailableMigrations(candidatePath);
        await using var connection = new SqliteConnection(CreateConnectionString(candidatePath, SqliteOpenMode.ReadOnly));
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var quickCheckPassed = await QuickCheckPassedAsync(connection, cancellationToken).ConfigureAwait(false);
        var foreignKeyViolationCount = await CountRowsAsync(
            connection,
            "PRAGMA foreign_key_check;",
            cancellationToken).ConfigureAwait(false);
        var historyTableExists = await TableExistsAsync(
            connection,
            "__EFMigrationsHistory",
            cancellationToken).ConfigureAwait(false);
        var appliedMigrations = historyTableExists
            ? await ReadAppliedMigrationsAsync(connection, cancellationToken).ConfigureAwait(false)
            : [];
        var migrationState = DetermineMigrationState(availableMigrations, appliedMigrations, historyTableExists);
        var migrationLockSuspected = await MigrationLockSuspectedAsync(connection, cancellationToken).ConfigureAwait(false);
        var schemaMetadataValid = await RequiredSchemaMetadataValidAsync(connection, cancellationToken).ConfigureAwait(false);

        return new BackupValidationReport(
            quickCheckPassed &&
            foreignKeyViolationCount == 0 &&
            string.Equals(migrationState, "current", StringComparison.Ordinal) &&
            !migrationLockSuspected &&
            schemaMetadataValid,
            migrationState);
    }

    private static string[] GetAvailableMigrations(string databasePath)
    {
        using var context = new HeroPassportDbContextFactory(databasePath).CreateDbContext();
        return context.Database.GetMigrations().ToArray();
    }

    private static async Task<bool> RequiredSchemaMetadataValidAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        string[] requiredTables =
        [
            "heroes",
            "projects",
            "app_settings",
            "quest_sessions",
            "mutation_receipts",
            "hero_project_stats",
            "quest_reports",
            "xp_events",
            "skills",
            "hero_skills",
            "quest_report_skills",
            "quest_reward_components",
            "quest_trust_strain_components",
            "traits",
            "hero_traits",
            "titles",
            "hero_titles",
            "quest_milestones",
        ];

        foreach (var table in requiredTables)
        {
            if (!await TableExistsAsync(connection, table, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }
        }

        return true;
    }

    private static async Task<bool> MigrationLockSuspectedAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, "__EFMigrationsLock", cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM \"__EFMigrationsLock\";";
        return Convert.ToInt64(
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
            CultureInfo.InvariantCulture) > 0;
    }

    private static async Task<bool> TableExistsAsync(
        SqliteConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM sqlite_master WHERE type='table' AND name=$name);";
        command.Parameters.AddWithValue("$name", tableName);
        return Convert.ToInt64(
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
            CultureInfo.InvariantCulture) == 1;
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
        string[] availableMigrations,
        string[] appliedMigrations,
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

        if (appliedMigrations.Length <= availableMigrations.Length &&
            appliedMigrations.SequenceEqual(availableMigrations.Take(appliedMigrations.Length), StringComparer.Ordinal))
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

    private static string CreateConnectionString(string path, SqliteOpenMode mode)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = mode,
            Cache = SqliteCacheMode.Default,
            Pooling = false,
            DefaultTimeout = 5,
        };
        return builder.ToString();
    }

    private static void TryDeleteUnpublishedCandidate(string temporaryPath)
    {
        try
        {
            File.Delete(temporaryPath);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private sealed record BackupValidationReport(bool Validated, string MigrationState);
}
