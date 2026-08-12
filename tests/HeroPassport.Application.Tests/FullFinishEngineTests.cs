using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using HeroPassport.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using System.Globalization;
using Xunit;

namespace HeroPassport.Application.Tests;

public sealed class FullFinishEngineTests
{
    [Fact]
    public async Task CleanObservedCodingQuestCommitsFullDeterministicProgressionAtomically()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, cancellationToken);
            var application = CreateApplication(path);
            var bootstrap = await application.BootstrapAsync(
                new BootstrapRequest(MutationRequestId.New(), "en-US", "Nova", "rpg_engineering", true, true),
                cancellationToken);
            var project = new ProjectBindingContext("Project", new string('a', 64), "project-identity/1");
            var start = await application.StartQuestAsync(
                new StartQuestRequest(MutationRequestId.New(), bootstrap.Hero.HeroId, "coding", "Full engine", "Exercise the complete deterministic RPG finish pipeline."),
                project,
                cancellationToken);

            var request = new FinishQuestRequest(
                MutationRequestId.New(),
                start.Quest.QuestId,
                "success",
                "Implemented the complete deterministic progression pipeline with directly observed passing tests.",
                new FinishMetrics(true, 0, 0, "passed", "observed", "passed", "observed"),
                ["coding", "testing_awareness", "scope_control"]);

            var result = await application.FinishQuestAsync(request, project, cancellationToken);

            Assert.Equal(95, result.Reward.XpGained);
            Assert.Equal(35, result.Reward.BonusXp);
            Assert.Equal(0, result.Reward.PenaltyXp);
            Assert.Equal(
                ["reward.base", "reward.observed_tests_passed", "reward.clean_scope", "reward.summary", "reward.no_user_corrections"],
                result.Reward.Components.Select(static component => component.Key));
            Assert.Equal([60, 10, 10, 10, 5], result.Reward.Components.Select(static component => component.XpDelta));

            Assert.Equal(0L, result.HeroProgress.TotalXpBefore);
            Assert.Equal(95L, result.HeroProgress.TotalXpAfter);
            Assert.Equal(1, result.HeroProgress.LevelBefore);
            Assert.Equal(1, result.HeroProgress.LevelAfter);
            Assert.False(result.HeroProgress.IsLevelCapped);
            Assert.Equal(95L, result.HeroProgress.LevelXp);
            Assert.Equal(100L, result.HeroProgress.NextLevelXpRequired);
            Assert.Equal("code_squire", result.HeroProgress.RankAfter);

            Assert.Equal(50, result.TrustStrain.TrustBefore);
            Assert.Equal(52, result.TrustStrain.TrustAfter);
            Assert.Equal(20, result.TrustStrain.StrainBefore);
            Assert.Equal(18, result.TrustStrain.StrainAfter);
            Assert.Equal(0, result.Streak.Before);
            Assert.Equal(1, result.Streak.After);

            Assert.Equal([47, 29, 19], result.SkillProgress.Select(static skill => skill.XpGained));
            Assert.Equal([47L, 29L, 19L], result.SkillProgress.Select(static skill => skill.XpAfter));
            Assert.All(result.SkillProgress, static skill => Assert.Equal(1, skill.LevelAfter));
            Assert.Empty(result.TraitsUnlocked);
            Assert.Empty(result.TitlesUnlocked);
            Assert.Null(result.ActiveTitle);
            Assert.Empty(result.Milestones);

            Assert.Equal(5L, await CountRowsAsync(path, "quest_reward_components", cancellationToken));
            Assert.Equal(3L, await CountRowsAsync(path, "quest_report_skills", cancellationToken));
            Assert.Equal(2L, await CountRowsAsync(path, "quest_trust_strain_components", cancellationToken));
            Assert.Equal(1L, await CountRowsAsync(path, "xp_events", cancellationToken));

            var card = await application.GetHeroCardAsync(bootstrap.Hero.HeroId, project, cancellationToken);
            Assert.Equal(95L, card.Hero.TotalXp);
            Assert.Equal(1, card.Hero.Level);
            Assert.Equal(95L, card.Hero.LevelXp);
            Assert.Equal(100L, card.Hero.NextLevelXpRequired);
            Assert.Equal(52, card.Hero.Trust);
            Assert.Equal(18, card.Hero.Strain);
            Assert.Equal(1, card.Hero.SuccessStreak);
            Assert.Equal("coding", card.Hero.TopSkills[0].SkillKey);
            Assert.Equal(47L, card.Hero.TopSkills[0].Xp);
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task SecondCleanQuestCrossesHeroAndSkillLevelsAndReplayReturnsPersistedProgression()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, cancellationToken);
            var application = CreateApplication(path);
            var bootstrap = await application.BootstrapAsync(
                new BootstrapRequest(MutationRequestId.New(), "en-US", "Nova", "rpg_engineering", true, true),
                cancellationToken);
            var project = new ProjectBindingContext("Project", new string('b', 64), "project-identity/1");

            await CompleteCleanQuestAsync(application, bootstrap.Hero.HeroId, project, cancellationToken);
            var second = await StartCleanQuestAsync(application, bootstrap.Hero.HeroId, project, cancellationToken);
            var finishRequest = CleanFinish(second.Quest.QuestId);
            var result = await application.FinishQuestAsync(finishRequest, project, cancellationToken);
            var replay = await application.FinishQuestAsync(finishRequest, project, cancellationToken);

            Assert.Equal(190L, result.HeroProgress.TotalXpAfter);
            Assert.Equal(1, result.HeroProgress.LevelBefore);
            Assert.Equal(2, result.HeroProgress.LevelAfter);
            Assert.Equal(90L, result.HeroProgress.LevelXp);
            Assert.Equal(150L, result.HeroProgress.NextLevelXpRequired);
            Assert.Contains(result.Milestones, static milestone => milestone.EventKey == "hero.level_up" && milestone.SemanticKey == "hero.level.2");
            Assert.Contains(result.SkillProgress, static skill => skill.SkillKey == "coding" && skill.LevelBefore == 1 && skill.LevelAfter == 2);
            Assert.Contains(result.SkillProgress, static skill => skill.SkillKey == "testing_awareness" && skill.LevelBefore == 1 && skill.LevelAfter == 2);
            Assert.Equal(result with { Replayed = true, AlreadyFinalized = true }, replay);
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    private static async Task CompleteCleanQuestAsync(
        HeroPassportApplication application,
        HeroId heroId,
        ProjectBindingContext project,
        CancellationToken cancellationToken)
    {
        var start = await StartCleanQuestAsync(application, heroId, project, cancellationToken);
        await application.FinishQuestAsync(CleanFinish(start.Quest.QuestId), project, cancellationToken);
    }

    private static Task<StartQuestResult> StartCleanQuestAsync(
        HeroPassportApplication application,
        HeroId heroId,
        ProjectBindingContext project,
        CancellationToken cancellationToken) =>
        application.StartQuestAsync(
            new StartQuestRequest(MutationRequestId.New(), heroId, "coding", "Clean coding", "Complete a clean coding Quest with observed passing tests."),
            project,
            cancellationToken);

    private static FinishQuestRequest CleanFinish(QuestId questId) =>
        new(
            MutationRequestId.New(),
            questId,
            "success",
            "Completed the clean coding Quest and directly observed the passing test result for this implementation.",
            new FinishMetrics(true, 0, 0, "passed", "observed", "passed", "observed"),
            ["coding", "testing_awareness", "scope_control"]);

    private static HeroPassportApplication CreateApplication(string databasePath) =>
        new(new SqliteHeroPassportStateStore(databasePath), new FixedTimeProvider(new DateTimeOffset(2026, 8, 13, 1, 0, 0, TimeSpan.Zero)));

    private static async Task<long> CountRowsAsync(string path, string table, CancellationToken cancellationToken)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(path, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table};";
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
    }

    private static string CreateDatabasePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "hero-passport-full-finish-tests", Guid.NewGuid().ToString("N"));
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

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
