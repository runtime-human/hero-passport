using HeroPassport.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using System.Globalization;
using Xunit;

namespace HeroPassport.Application.Tests;

public sealed class UnlockSchemaTests
{
    [Fact]
    public async Task DatabaseSeedsCanonicalUnlockCatalogsAndHistoryTables()
    {
        var token = TestContext.Current.CancellationToken;
        var path = TestRuntime.CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);

            Assert.Equal(
                5,
                await ScalarLongAsync(
                    path,
                    "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('traits','titles','hero_traits','hero_titles','quest_milestones');",
                    token));

            Assert.Equal(
                [
                    "polyglot_crafter:unlock-catalog/1",
                    "precise_executor:unlock-catalog/1",
                    "scope_keeper:unlock-catalog/1",
                    "steady_hand:unlock-catalog/1",
                    "test_scout:unlock-catalog/1",
                ],
                await StringsAsync(
                    path,
                    "SELECT trait_key || ':' || catalog_version FROM traits ORDER BY trait_key;",
                    token));

            Assert.Equal(
                [
                    "master_of_many_tools:5:unlock-catalog/1",
                    "unbroken_builder:4:unlock-catalog/1",
                    "skill_specialist:3:unlock-catalog/1",
                    "veteran_of_the_merge:2:unlock-catalog/1",
                    "rising_adventurer:1:unlock-catalog/1",
                ],
                await StringsAsync(
                    path,
                    "SELECT title_key || ':' || priority || ':' || catalog_version FROM titles ORDER BY priority DESC;",
                    token));
        }
        finally
        {
            TestRuntime.DeleteDatabase(path);
        }
    }

    private static async Task<long> ScalarLongAsync(string path, string sql, CancellationToken token)
    {
        await using var connection = await OpenAsync(path, token);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync(token), CultureInfo.InvariantCulture);
    }

    private static async Task<string[]> StringsAsync(string path, string sql, CancellationToken token)
    {
        await using var connection = await OpenAsync(path, token);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(token);
        var values = new List<string>();
        while (await reader.ReadAsync(token))
        {
            values.Add(reader.GetString(0));
        }

        return [.. values];
    }

    private static async Task<SqliteConnection> OpenAsync(string path, CancellationToken token)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false,
        }.ToString());
        await connection.OpenAsync(token);
        return connection;
    }
}
