using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace HeroPassport.Infrastructure.Persistence;

public static class HeroPassportDatabase
{
    public static async Task InitializeAsync(string databasePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        var fullPath = Path.GetFullPath(databasePath);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("Database path must have a parent directory.", nameof(databasePath));
        Directory.CreateDirectory(directory);

        await using (var connection = await OpenConnectionAsync(fullPath, cancellationToken).ConfigureAwait(false))
        {
            await EnsureSupportedSqliteAsync(connection, cancellationToken).ConfigureAwait(false);
            await SetWalAsync(connection, cancellationToken).ConfigureAwait(false);
        }

        var factory = new HeroPassportDbContextFactory(fullPath);
        await using var context = factory.CreateDbContext();
        await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task<byte[]> ReadProjectIdentitySaltAsync(
        string databasePath,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(databasePath, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT project_identity_salt_v1 FROM app_settings WHERE id=1;";
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (value is not byte[] { Length: 32 } salt)
        {
            throw new InvalidOperationException("Hero Passport project identity salt is unavailable.");
        }

        return salt;
    }

    public static async Task<SqliteConnection> OpenConnectionAsync(
        string databasePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        var connection = new SqliteConnection(CreateConnectionString(databasePath));
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await SqliteConnectionPolicy.ApplyAsync(connection, cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    internal static string CreateConnectionString(string databasePath)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(databasePath),
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Default,
            ForeignKeys = true,
            Pooling = true,
            DefaultTimeout = 5,
        };

        return builder.ToString();
    }

    private static async Task EnsureSupportedSqliteAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT sqlite_version();";
        var versionText = Convert.ToString(
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
            CultureInfo.InvariantCulture)
            ?? throw new InvalidOperationException("SQLite did not report a runtime version.");

        var version = ParseSqliteVersion(versionText);
        if (version < new Version(3, 53, 4))
        {
            throw new NotSupportedException($"SQLite runtime {version} is unsupported. Hero Passport requires SQLite 3.53.4 or newer.");
        }
    }

    private static async Task SetWalAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL;";
        var mode = Convert.ToString(
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
            CultureInfo.InvariantCulture);

        if (!string.Equals(mode, "wal", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"SQLite refused WAL mode. Effective journal mode: {mode ?? "<null>"}.");
        }
    }

    private static Version ParseSqliteVersion(string value)
    {
        var components = value.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (components.Length < 3 ||
            !int.TryParse(components[0], NumberStyles.None, CultureInfo.InvariantCulture, out var major) ||
            !int.TryParse(components[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minor) ||
            !int.TryParse(components[2], NumberStyles.None, CultureInfo.InvariantCulture, out var build))
        {
            throw new InvalidOperationException($"Unrecognized SQLite version '{value}'.");
        }

        return new Version(major, minor, build);
    }
}
