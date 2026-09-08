using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using HeroPassport.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Xunit;

namespace HeroPassport.Application.Tests;

public sealed class HeroLogicalDeleteBehaviorTests
{
    [Fact]
    public async Task PermanentDeleteGuardsActiveAndOpenHeroThenTombstonesReceiptsAndBlocksLateRetries()
    {
        var token = TestContext.Current.CancellationToken;
        var path = TestRuntime.CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            var app = TestRuntime.CreateApplication(path);
            var active = (await app.BootstrapAsync(
                new BootstrapRequest(MutationRequestId.New(), "en-US", "Active", "rpg_engineering", true, true),
                token)).Hero;

            var createRequestId = MutationRequestId.New();
            var created = await app.CreateHeroAsync(new CreateHeroRequest(createRequestId, "Disposable"), token);
            var heroId = created.Hero.HeroId;

            var activeError = await Assert.ThrowsAsync<HeroPassportException>(
                () => app.DeleteHeroPermanentlyAsync(active.HeroId, token));
            Assert.Equal("HP145", activeError.Code);

            var project = new ProjectBindingContext("Project", new string('d', 64), "project-identity/1");
            var startRequestId = MutationRequestId.New();
            var startRequest = new StartQuestRequest(
                startRequestId,
                heroId,
                "coding",
                "Disposable quest",
                "Create history that must disappear with the logically deleted Hero.");
            var started = await app.StartQuestAsync(startRequest, project, token);

            var openError = await Assert.ThrowsAsync<HeroPassportException>(
                () => app.DeleteHeroPermanentlyAsync(heroId, token));
            Assert.Equal("HP143", openError.Code);

            var finishRequestId = MutationRequestId.New();
            var finishRequest = new FinishQuestRequest(
                finishRequestId,
                started.Quest.QuestId,
                "success",
                "Finish the Quest so delete must remove canonical history and projections atomically.",
                new FinishQuestMetrics(false, 0, 0, "not_run", "none", "not_run", "none"),
                ["coding"]);
            await app.FinishQuestAsync(finishRequest, project, token);

            await app.DeleteHeroPermanentlyAsync(heroId, token);

            var heroes = await app.ListHeroesAsync(token);
            Assert.DoesNotContain(heroes.Heroes, hero => hero.HeroId == heroId);

            var createReplay = await Assert.ThrowsAsync<HeroPassportException>(
                () => app.CreateHeroAsync(new CreateHeroRequest(createRequestId, "Disposable"), token));
            Assert.Equal("HP140", createReplay.Code);

            var startReplay = await Assert.ThrowsAsync<HeroPassportException>(
                () => app.StartQuestAsync(startRequest, project, token));
            Assert.Equal("HP140", startReplay.Code);

            var finishReplay = await Assert.ThrowsAsync<HeroPassportException>(
                () => app.FinishQuestAsync(finishRequest, project, token));
            Assert.Equal("HP140", finishReplay.Code);

            await using var connection = new SqliteConnection($"Data Source={path}");
            await connection.OpenAsync(token);

            Assert.Equal(0L, await ScalarAsync(connection, "SELECT COUNT(*) FROM heroes WHERE id=$hero;", heroId.ToString(), token));
            Assert.Equal(0L, await ScalarAsync(connection, "SELECT COUNT(*) FROM quest_sessions WHERE hero_id=$hero;", heroId.ToString(), token));
            Assert.Equal(0L, await ScalarAsync(connection, "SELECT COUNT(*) FROM hero_project_stats WHERE hero_id=$hero;", heroId.ToString(), token));
            Assert.Equal(0L, await ScalarAsync(connection, "SELECT COUNT(*) FROM hero_skills WHERE hero_id=$hero;", heroId.ToString(), token));
            Assert.Equal(0L, await ScalarAsync(connection, "SELECT COUNT(*) FROM xp_events WHERE hero_id=$hero;", heroId.ToString(), token));
            Assert.Equal(3L, await ScalarAsync(connection, "SELECT COUNT(*) FROM mutation_receipts WHERE hero_id=$hero AND result_status='target_deleted';", heroId.ToString(), token));
            Assert.Equal(0L, await ScalarAsync(connection, "SELECT COUNT(*) FROM mutation_receipts WHERE hero_id=$hero AND result_status='active';", heroId.ToString(), token));

            await using var foreignKeyCheck = connection.CreateCommand();
            foreignKeyCheck.CommandText = "PRAGMA foreign_key_check;";
            await using var reader = await foreignKeyCheck.ExecuteReaderAsync(token);
            Assert.False(await reader.ReadAsync(token));
        }
        finally
        {
            TestRuntime.DeleteDatabase(path);
        }
    }

    private static async Task<long> ScalarAsync(
        SqliteConnection connection,
        string sql,
        string heroId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$hero", heroId);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);
    }
}
