using HeroPassport.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using Xunit;

namespace HeroPassport.Infrastructure.Tests;

public sealed class SqlitePersistenceTests
{
    [Fact]
    public async Task InitializeCreatesQualifiedFileBackedDatabase()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, cancellationToken);

            await using var connection = await HeroPassportDatabase.OpenConnectionAsync(path, cancellationToken);
            Assert.Equal("wal", await ScalarStringAsync(connection, "PRAGMA journal_mode;", cancellationToken));
            Assert.Equal(2L, await ScalarLongAsync(connection, "PRAGMA synchronous;", cancellationToken));
            Assert.Equal(1L, await ScalarLongAsync(connection, "PRAGMA foreign_keys;", cancellationToken));
            Assert.Equal(0L, await ScalarLongAsync(connection, "PRAGMA trusted_schema;", cancellationToken));
            Assert.True(ParseSqliteVersion(await ScalarStringAsync(connection, "SELECT sqlite_version();", cancellationToken)) >= new Version(3, 53, 4));
            Assert.Equal(1L, await ScalarLongAsync(connection, "SELECT COUNT(*) FROM app_settings WHERE id = 1;", cancellationToken));

            var factory = new HeroPassportDbContextFactory(path);
            await using var context = factory.CreateDbContext();
            Assert.NotEmpty(await context.Database.GetAppliedMigrationsAsync(cancellationToken));
            Assert.Empty(await context.Database.GetPendingMigrationsAsync(cancellationToken));
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task PhysicalChecksRejectInvalidState()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, cancellationToken);
            var factory = new HeroPassportDbContextFactory(path);

            await using (var context = factory.CreateDbContext())
            {
                context.Set<Dictionary<string, object>>("HeroPassport.Storage.AppSettings").Add(new()
                {
                    ["id"] = 2,
                    ["setup_completed"] = 0,
                    ["locale"] = "en-US",
                    ["presentation_style"] = "rpg_engineering",
                    ["auto_start_quest"] = 1,
                    ["auto_finish_quest"] = 1,
                    ["project_identity_salt_v1"] = new byte[32],
                    ["config_version"] = 1,
                    ["created_at_utc"] = "2026-08-11T00:00:00.000Z",
                    ["updated_at_utc"] = "2026-08-11T00:00:00.000Z",
                });
                await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(cancellationToken));
            }

            await using (var context = factory.CreateDbContext())
            {
                context.Set<Dictionary<string, object>>("HeroPassport.Storage.Hero").Add(NewHero(
                    "01900000-0000-7000-8000-000000000001",
                    trust: 101));
                await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(cancellationToken));
            }
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task PartialIndexAllowsOnlyOneOpenQuestPerHeroAndProject()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, cancellationToken);
            var factory = new HeroPassportDbContextFactory(path);
            const string heroId = "01900000-0000-7000-8000-000000000001";
            const string projectId = "01900000-0000-7000-8000-000000000002";

            await using var context = factory.CreateDbContext();
            context.Set<Dictionary<string, object>>("HeroPassport.Storage.Hero").Add(NewHero(heroId));
            context.Set<Dictionary<string, object>>("HeroPassport.Storage.Project").Add(new()
            {
                ["id"] = projectId,
                ["display_name"] = "Project",
                ["workspace_fingerprint"] = new string('a', 64),
                ["identity_version"] = "project-identity/1",
                ["created_at_utc"] = "2026-08-11T00:00:00.000Z",
            });
            context.Set<Dictionary<string, object>>("HeroPassport.Storage.QuestSession").Add(NewOpenQuest(
                "01900000-0000-7000-8000-000000000003",
                heroId,
                projectId,
                "2026-08-11T00:00:00.000Z"));
            await context.SaveChangesAsync(cancellationToken);

            context.Set<Dictionary<string, object>>("HeroPassport.Storage.QuestSession").Add(NewOpenQuest(
                "01900000-0000-7000-8000-000000000004",
                heroId,
                projectId,
                "2026-08-11T00:00:01.000Z"));
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(cancellationToken));
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    private static Dictionary<string, object> NewHero(string id, int trust = 50) => new()
    {
        ["id"] = id,
        ["name"] = "Hero",
        ["total_xp"] = 0L,
        ["trust"] = trust,
        ["strain"] = 20,
        ["success_streak"] = 0L,
        ["created_at_utc"] = "2026-08-11T00:00:00.000Z",
        ["updated_at_utc"] = "2026-08-11T00:00:00.000Z",
    };

    private static Dictionary<string, object> NewOpenQuest(string id, string heroId, string projectId, string startedAt) => new()
    {
        ["id"] = id,
        ["hero_id"] = heroId,
        ["project_id"] = projectId,
        ["quest_type"] = "coding",
        ["title"] = "Quest",
        ["goal"] = "Goal",
        ["locale"] = "en-US",
        ["status"] = "open",
        ["started_at_utc"] = startedAt,
        ["created_at_utc"] = startedAt,
    };

    private static async Task<long> ScalarLongAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
    }

    private static async Task<string> ScalarStringAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture)!;
    }

    private static Version ParseSqliteVersion(string value)
    {
        var parts = value.Split('.');
        return new Version(
            int.Parse(parts[0], CultureInfo.InvariantCulture),
            int.Parse(parts[1], CultureInfo.InvariantCulture),
            int.Parse(parts[2], CultureInfo.InvariantCulture));
    }

    private static string CreateDatabasePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "hero-passport-tests", Guid.NewGuid().ToString("N"));
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
