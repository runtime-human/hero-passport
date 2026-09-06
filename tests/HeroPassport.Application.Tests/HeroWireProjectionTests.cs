using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using HeroPassport.Infrastructure.Persistence;
using Xunit;

namespace HeroPassport.Application.Tests;

public sealed class HeroWireProjectionTests
{
    [Fact]
    public async Task FinishReplayUsesImmutableProgressSnapshotsWhileCardUsesCurrentProjection()
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
            var project = new ProjectBindingContext("Project", new string('c', 64), "project-identity/1");
            var quest = (await app.StartQuestAsync(
                new StartQuestRequest(MutationRequestId.New(), hero.HeroId, "coding", "Quest", "Verify immutable wire projections across retries"),
                project,
                token)).Quest;
            var request = new FinishQuestRequest(
                MutationRequestId.New(),
                quest.QuestId,
                "success",
                "Complete the Quest while preserving historical wire snapshots across future projection changes.",
                new FinishQuestMetrics(false, 0, 0, "not_run", "none", "not_run", "none"),
                ["coding"]);

            var committed = await app.FinishQuestAsync(request, project, token);
            Assert.False(committed.HeroProgress.IsLevelCapped);
            Assert.Equal(85, committed.HeroProgress.LevelXp);
            Assert.Equal(100, committed.HeroProgress.NextLevelXpRequired);
            Assert.Equal(50, committed.TrustStrain.TrustBefore);
            Assert.Equal(52, committed.TrustStrain.TrustAfter);
            Assert.Equal(20, committed.TrustStrain.StrainBefore);
            Assert.Equal(18, committed.TrustStrain.StrainAfter);
            Assert.Collection(
                committed.TrustStrain.Components,
                component => Assert.Equal(new TrustStrainComponentSnapshot("success_outcome", 1, -1), component),
                component => Assert.Equal(new TrustStrainComponentSnapshot("clean_success_bonus", 1, -1), component));
            Assert.Equal("trust-strain/1.0.0", committed.TrustStrain.RuleVersion);
            Assert.Equal(0, committed.Streak.Before);
            Assert.Equal(1, committed.Streak.After);
            Assert.Equal("streak/1.0.0", committed.Streak.RuleVersion);
            var skill = Assert.Single(committed.SkillProgress);
            Assert.Equal("coding", skill.SkillKey);
            Assert.Equal(85, skill.XpGained);
            Assert.Equal(85, skill.XpAfter);
            Assert.Equal(1, skill.LevelBefore);
            Assert.Equal(2, skill.LevelAfter);
            Assert.False(skill.IsLevelCapped);
            Assert.Equal(75L, skill.NextLevelXpRequired);
            Assert.Empty(committed.TraitsUnlocked);
            Assert.Empty(committed.TitlesUnlocked);
            Assert.Null(committed.ActiveTitle);
            Assert.Empty(committed.Milestones);

            await using (var connection = await HeroPassportDatabase.OpenConnectionAsync(path, token))
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "UPDATE heroes SET trust=80,strain=30,success_streak=3 WHERE id=$hero;";
                command.Parameters.AddWithValue("$hero", hero.HeroId.ToString());
                await command.ExecuteNonQueryAsync(token);
            }

            var replay = await app.FinishQuestAsync(request, project, token);
            Assert.True(replay.Replayed);
            Assert.Equal(committed.TrustStrain.TrustBefore, replay.TrustStrain.TrustBefore);
            Assert.Equal(committed.TrustStrain.TrustAfter, replay.TrustStrain.TrustAfter);
            Assert.Equal(committed.TrustStrain.StrainBefore, replay.TrustStrain.StrainBefore);
            Assert.Equal(committed.TrustStrain.StrainAfter, replay.TrustStrain.StrainAfter);
            Assert.Equal(committed.TrustStrain.RuleVersion, replay.TrustStrain.RuleVersion);
            Assert.True(committed.TrustStrain.Components.SequenceEqual(replay.TrustStrain.Components));
            Assert.Equal(committed.Streak, replay.Streak);
            Assert.Equal(committed.HeroProgress, replay.HeroProgress);
            Assert.True(committed.SkillProgress.SequenceEqual(replay.SkillProgress));

            var card = await app.GetCardAsync(hero.HeroId, project, token);
            Assert.False(card.Hero.IsLevelCapped);
            Assert.Equal(85, card.Hero.LevelXp);
            Assert.Equal(100, card.Hero.NextLevelXpRequired);
            Assert.Null(card.Hero.ActiveTitle);
            Assert.Equal(80, card.Hero.Trust);
            Assert.Equal(30, card.Hero.Strain);
            Assert.Equal(3, card.Hero.SuccessStreak);
        }
        finally
        {
            TestRuntime.DeleteDatabase(path);
        }
    }
}
