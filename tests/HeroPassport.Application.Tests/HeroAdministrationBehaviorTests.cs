using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using HeroPassport.Infrastructure.Persistence;
using Xunit;

namespace HeroPassport.Application.Tests;

public sealed class HeroAdministrationBehaviorTests
{
    [Fact]
    public async Task ListOrdersActiveThenNonArchivedAndActivationIsIdempotent()
    {
        var token = TestContext.Current.CancellationToken;
        var path = TestRuntime.CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            var app = TestRuntime.CreateApplication(path);
            var first = (await app.BootstrapAsync(new BootstrapRequest(MutationRequestId.New(), "en-US", "First", "rpg_engineering", true, true), token)).Hero;
            var second = (await app.CreateHeroAsync(new CreateHeroRequest(MutationRequestId.New(), "Second"), token)).Hero;

            var initial = await app.ListHeroesAsync(token);
            Assert.Equal([first.HeroId, second.HeroId], initial.Heroes.Select(static h => h.HeroId).ToArray());
            Assert.True(initial.Heroes[0].Active);
            Assert.False(initial.Heroes[1].Active);

            var activated = await app.ActivateHeroPreferenceAsync(second.HeroId, token);
            var repeated = await app.ActivateHeroPreferenceAsync(second.HeroId, token);
            Assert.True(activated.Changed);
            Assert.False(repeated.Changed);

            var reordered = await app.ListHeroesAsync(token);
            Assert.Equal(second.HeroId, reordered.Heroes[0].HeroId);
            Assert.True(reordered.Heroes[0].Active);
        }
        finally
        {
            TestRuntime.DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task ArchiveGuardsActiveAndOpenQuestAndRestoreIsIdempotent()
    {
        var token = TestContext.Current.CancellationToken;
        var path = TestRuntime.CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            var app = TestRuntime.CreateApplication(path);
            var first = (await app.BootstrapAsync(new BootstrapRequest(MutationRequestId.New(), "en-US", "First", "rpg_engineering", true, true), token)).Hero;
            var second = (await app.CreateHeroAsync(new CreateHeroRequest(MutationRequestId.New(), "Second"), token)).Hero;

            var activeError = await Assert.ThrowsAsync<HeroPassportException>(() => app.ArchiveHeroAsync(first.HeroId, token));
            Assert.Equal("HP145", activeError.Code);

            var project = new ProjectBindingContext("Project", new string('a', 64), "project-identity/1");
            await app.StartQuestAsync(new StartQuestRequest(MutationRequestId.New(), second.HeroId, "coding", "Open", "Keep the second Hero busy"), project, token);
            var openError = await Assert.ThrowsAsync<HeroPassportException>(() => app.ArchiveHeroAsync(second.HeroId, token));
            Assert.Equal("HP143", openError.Code);

            await app.FinishQuestAsync(
                new FinishQuestRequest(
                    MutationRequestId.New(),
                    (await app.GetRuntimeContextAsync(project, token)).OpenQuests.Single().QuestId,
                    "success",
                    "Finish the open Quest before archiving the inactive Hero safely.",
                    new FinishQuestMetrics(false, 0, 0, "not_run", "none", "not_run", "none"),
                    ["coding"]),
                project,
                token);

            var archived = await app.ArchiveHeroAsync(second.HeroId, token);
            var archivedAgain = await app.ArchiveHeroAsync(second.HeroId, token);
            Assert.True(archived.Changed);
            Assert.False(archivedAgain.Changed);
            Assert.True(archived.Hero.Archived);

            var restored = await app.RestoreHeroAsync(second.HeroId, token);
            var restoredAgain = await app.RestoreHeroAsync(second.HeroId, token);
            Assert.True(restored.Changed);
            Assert.False(restoredAgain.Changed);
            Assert.False(restored.Hero.Archived);
        }
        finally
        {
            TestRuntime.DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task ExplicitCardReturnsHeroAndCurrentProjectProjection()
    {
        var token = TestContext.Current.CancellationToken;
        var path = TestRuntime.CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            var app = TestRuntime.CreateApplication(path);
            var hero = (await app.BootstrapAsync(new BootstrapRequest(MutationRequestId.New(), "en-US", "Nova", "rpg_engineering", true, true), token)).Hero;
            var project = new ProjectBindingContext("Project", new string('b', 64), "project-identity/1");
            var quest = (await app.StartQuestAsync(new StartQuestRequest(MutationRequestId.New(), hero.HeroId, "coding", "Quest", "Produce a minimal card projection"), project, token)).Quest;
            await app.FinishQuestAsync(
                new FinishQuestRequest(
                    MutationRequestId.New(),
                    quest.QuestId,
                    "success",
                    "Finish the Quest so the card exposes the persisted Project counters and XP.",
                    new FinishQuestMetrics(false, 0, 0, "not_run", "none", "not_run", "none"),
                    ["coding"]),
                project,
                token);

            var card = await app.GetCardAsync(hero.HeroId, project, token);

            Assert.Equal(hero.HeroId, card.Hero.HeroId);
            Assert.Equal(60, card.Hero.TotalXp);
            Assert.Equal(1, card.Hero.Level);
            Assert.Equal("code_squire", card.Hero.RankKey);
            Assert.Empty(card.Hero.TopSkills);
            Assert.Empty(card.Hero.Traits);
            Assert.Empty(card.Hero.Titles);
            Assert.Equal("Project", card.Project.DisplayName);
            Assert.Equal(1, card.Project.QuestsStarted);
            Assert.Equal(1, card.Project.QuestsFinished);
            Assert.Equal(1, card.Project.QuestsSucceeded);
            Assert.Equal(60, card.Project.TotalXpEarned);
            Assert.Equal(1000, card.Project.SuccessRatePermille);
        }
        finally
        {
            TestRuntime.DeleteDatabase(path);
        }
    }
}
