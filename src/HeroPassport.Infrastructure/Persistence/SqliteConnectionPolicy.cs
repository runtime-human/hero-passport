using Microsoft.Data.Sqlite;
using System.Data.Common;

namespace HeroPassport.Infrastructure.Persistence;

internal static class SqliteConnectionPolicy
{
    private const string ConnectionPragmas = "PRAGMA foreign_keys=ON; PRAGMA synchronous=FULL; PRAGMA trusted_schema=OFF;";

    public static void Apply(DbConnection connection)
    {
        if (connection is not SqliteConnection sqliteConnection)
        {
            return;
        }

        using var command = sqliteConnection.CreateCommand();
        command.CommandText = ConnectionPragmas;
        try
        {
            command.ExecuteNonQuery();
        }
        catch (SqliteException exception)
        {
            WriteDiagnostics(sqliteConnection, exception);
            throw;
        }
    }

    public static async Task ApplyAsync(DbConnection connection, CancellationToken cancellationToken = default)
    {
        if (connection is not SqliteConnection sqliteConnection)
        {
            return;
        }

        await using var command = sqliteConnection.CreateCommand();
        command.CommandText = ConnectionPragmas;
        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqliteException exception)
        {
            WriteDiagnostics(sqliteConnection, exception);
            throw;
        }
    }

    private static void WriteDiagnostics(SqliteConnection connection, SqliteException exception)
    {
        var dataSource = connection.DataSource;
        static string State(string path)
        {
            try
            {
                var info = new FileInfo(path);
                return info.Exists ? $"present:{info.Length}" : "missing";
            }
            catch (Exception error)
            {
                return $"unavailable:{error.GetType().Name}";
            }
        }

        Console.Error.WriteLine(
            $"SQLITE_POLICY_DIAGNOSTIC code={exception.SqliteErrorCode} extended={exception.SqliteExtendedErrorCode} " +
            $"db={State(dataSource)} wal={State(dataSource + "-wal")} shm={State(dataSource + "-shm")}");
    }
}
