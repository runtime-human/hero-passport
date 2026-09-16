using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using HeroPassport.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using System.Globalization;
using Xunit;

namespace HeroPassport.Infrastructure.Tests;

public sealed class QuestHistoryReadTests
{
    private static readonly ProjectBindingContext UnseenProject =
        new("Unseen Project", new string('a', 64), "project-identity/1");

    [Fact]
    public async Task UnseenProjectHistoryIsEmptyAndDoesNotPersistProject()
    {
        var token = TestContext.Current.CancellationToken;
        var path = CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            var store = new SqliteHeroPassportStateStore(path);
            var before = await CountAsync(path, "projects", token);

            var history = await store.GetProjectQuestHistoryAsync(UnseenProject, token);

            Assert.Equal("Unseen Project", history.ProjectDisplayName);
            Assert.Empty(history.Items);
            Assert.Equal(before, await CountAsync(path, "projects", token));
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task UnseenProjectDetailIsNotFoundAndDoesNotPersistProject()
    {
        var token = TestContext.Current.CancellationToken;
        var path = CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            var store = new SqliteHeroPassportStateStore(path);
            var before = await CountAsync(path, "projects", token);

            var detail = await store.GetQuestHistoryDetailAsync(QuestId.New(), UnseenProject, token);

            Assert.Null(detail);
            Assert.Equal(before, await CountAsync(path, "projects", token));
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    private static async Task<long> CountAsync(string path, string table, CancellationToken token)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(path, token);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table};";
        return Convert.ToInt64(await command.ExecuteScalarAsync(token), CultureInfo.InvariantCulture);
    }

    private static string CreateDatabasePath()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "hero-passport-history-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "hero-passport.db");
    }

    private static void DeleteDatabase(string path)
    {
        SqliteConnection.ClearAllPools();
        var directory = Path.GetDirectoryName(path);
        if (directory is not null && Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
