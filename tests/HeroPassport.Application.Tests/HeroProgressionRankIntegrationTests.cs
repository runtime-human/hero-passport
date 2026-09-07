using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Engine;
using HeroPassport.Domain.Primitives;
using HeroPassport.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using System.Globalization;
using Xunit;

namespace HeroPassport.Application.Tests;

public sealed class HeroProgressionRankIntegrationTests
{
    [Fact]
    public async Task RealQuestProgressionCrossesLevelFiveRankBoundaryAndReplaysStoredFacts()
    {
        var token = TestContext.Current.CancellationToken;
        var path = TestRuntime.CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            var app = TestRuntime.CreateApplication(path);
            var hero = (await app.BootstrapAsync(
                new BootstrapRequest(MutationRequestId.New(), "en-US", "Nova", "rpg_engineering", true, true),
                token)).Hero;
            var project = new ProjectBindingContext("Hero Rank Qualification", new string('d', 64), "project-identity/1");

            FinishQuestResult? boundary = null;
            for (var questNumber = 1; questNumber <= 8; questNumber++)
            {
                var quest = (await app.StartQuestAsync(
                    new StartQuestRequest(
                        MutationRequestId.New(),
                        hero.HeroId,
                        "coding",
                        $"Hero progression qualification {questNumber}",
                        "Qualify the deterministic Hero level and Rank transition through normal Quest progression."),
                    project,
                    token)).Quest;

                var finished = await app.FinishQuestAsync(
                    FinishRequest(quest.QuestId),
                    project,
                    token);

                if (questNumber == 8)
                {
                    boundary = finished;
                }
            }

            boundary = Assert.IsType<FinishQuestResult>(boundary);
            Assert.Equal(665, boundary.HeroProgress.TotalXpBefore);
            Assert.Equal(760, boundary.HeroProgress.TotalXpAfter);
            Assert.Equal(4, boundary.HeroProgress.LevelBefore);
            Assert.Equal(5, boundary.HeroProgress.LevelAfter);
            Assert.Equal("code_squire", boundary.HeroProgress.RankBefore);
            Assert.Equal("code_knight", boundary.HeroProgress.RankAfter);
            Assert.Equal(HeroProgressionRules.RuleVersion, boundary.HeroProgress.HeroProgressionVersion);
            Assert.Equal(RankRules.RuleVersion, boundary.HeroProgress.RankRuleVersion);
            Assert.Contains(
                new MilestoneSnapshot("hero_level_changed", "hero_level:5"),
                boundary.Milestones);
            Assert.Contains(
                new MilestoneSnapshot("rank_changed", "rank:code_knight"),
                boundary.Milestones);

            Assert.Equal(
                $"{HeroProgressionRules.RuleVersion}:{RankRules.RuleVersion}:4:5:code_squire:code_knight",
                await ScalarStringAsync(
                    path,
                    $"SELECT hero_progression_version || ':' || rank_rule_version || ':' || hero_level_before || ':' || hero_level_after || ':' || rank_before || ':' || rank_after FROM quest_reports WHERE quest_id='{boundary.QuestId}';",
                    token));

            var replay = await app.FinishQuestAsync(
                FinishRequest(boundary.QuestId) with { FinishRequestId = MutationRequestId.New() },
                project,
                token);
            Assert.True(replay.AlreadyFinalized);
            Assert.Equal(boundary.HeroProgress, replay.HeroProgress);
            Assert.Equal(boundary.Milestones, replay.Milestones);

            var card = await app.GetCardAsync(hero.HeroId, project, token);
            Assert.Equal(760, card.Hero.TotalXp);
            Assert.Equal(5, card.Hero.Level);
            Assert.Equal("code_knight", card.Hero.RankKey);

            Assert.Equal(HeroProgressionRules.RuleVersion, HeroPassportVersions.CurrentRules.HeroProgression);
            Assert.Equal(RankRules.RuleVersion, HeroPassportVersions.CurrentRules.Rank);
            Assert.Equal(UnlockRules.RuleVersion, HeroPassportVersions.CurrentRules.Unlock);
        }
        finally
        {
            TestRuntime.DeleteDatabase(path);
        }
    }

    private static FinishQuestRequest FinishRequest(QuestId questId) =>
        new(
            MutationRequestId.New(),
            questId,
            "success",
            "Completed the Hero progression qualification Quest with observed tests and clean scope for deterministic progression.",
            new FinishQuestMetrics(
                TestsMentioned: true,
                ScopeViolations: 0,
                UserCorrections: 0,
                BuildStatus: "not_run",
                BuildEvidence: "none",
                TestsStatus: "passed",
                TestsEvidence: "observed"),
            ["coding"]);

    private static async Task<string> ScalarStringAsync(string path, string sql, CancellationToken token)
    {
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false,
        }.ToString());
        await connection.OpenAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(await command.ExecuteScalarAsync(token), CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
