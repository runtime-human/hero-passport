using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using HeroPassport.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Xunit;

namespace HeroPassport.Infrastructure.Tests;

public sealed class SkillProgressionReadTests
{
    private static readonly string[] CanonicalSkillOrder =
    [
        "coding",
        "testing_awareness",
        "scope_control",
        "documentation",
        "tool_use",
        "planning",
        "research",
        "debugging",
        "review",
        "maintenance",
    ];

    [Fact]
    public async Task SkillProgressionReturnsAllCanonicalSkillsIncludingZeroXpRows()
    {
        var token = TestContext.Current.CancellationToken;
        var path = CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            var app = CreateApplication(path);
            var hero = (await app.BootstrapAsync(
                new BootstrapRequest(
                    MutationRequestId.New(),
                    "en-US",
                    "Nova",
                    "rpg_engineering",
                    true,
                    true),
                token)).Hero;
            var project = new ProjectBindingContext(
                "Unseen Skill Project",
                new string('a', 64),
                "project-identity/1");

            var result = await app.GetSkillProgressionAsync(hero.HeroId, project, token);

            Assert.Equal("Nova", result.HeroName);
            Assert.Equal("Unseen Skill Project", result.ProjectDisplayName);
            Assert.Equal(CanonicalSkillOrder, result.Skills.Select(static skill => skill.SkillKey));
            Assert.Equal(10, result.Skills.Count);
            Assert.All(result.Skills, skill =>
            {
                AssertZeroProgress(skill.Hero);
                Assert.Equal(0, skill.ProjectContribution.Xp);
            });
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    private static void AssertZeroProgress(SkillProgressionReadSnapshot skill)
    {
        Assert.Equal(0, skill.Xp);
        Assert.Equal(1, skill.Level);
        Assert.False(skill.IsLevelCapped);
        Assert.Equal(0, skill.LevelXp);
        Assert.Equal(50, skill.NextLevelXpRequired);
    }

    private static HeroPassportApplication CreateApplication(string path) =>
        new(new SqliteHeroPassportStateStore(path), TimeProvider.System);

    private static string CreateDatabasePath()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "hero-passport-skill-progression-tests",
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
