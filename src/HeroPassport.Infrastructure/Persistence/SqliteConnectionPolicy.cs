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
        command.ExecuteNonQuery();
    }

    public static async Task ApplyAsync(DbConnection connection, CancellationToken cancellationToken = default)
    {
        if (connection is not SqliteConnection sqliteConnection)
        {
            return;
        }

        await using var command = sqliteConnection.CreateCommand();
        command.CommandText = ConnectionPragmas;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
