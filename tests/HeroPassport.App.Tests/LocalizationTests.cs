using HeroPassport.App.Localization;
using HeroPassport.App.Mcp;
using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using HeroPassport.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Xunit;

namespace HeroPassport.App.Tests;

public sealed class LocalizationTests
{
    [Theory]
    [InlineData("en-US", "quest.finished", "✓ Quest completed · +95 XP")]
    [InlineData("ru-RU", "quest.finished", "✓ Квест завершён · +95 XP")]
    public void ResourceCatalogResolvesExplicitSupportedCulture(string locale, string key, string expected)
    {
        Assert.Equal(expected, HeroPassportTextCatalog.Format(locale, key, 95));
    }

    [Fact]
    public async Task FinishPresentationUsesPersistedQuestLocaleAfterGlobalPreferenceChanges()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var path = CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, cancellationToken);
            var application = new HeroPassportApplication(
                new SqliteHeroPassportStateStore(path),
                new FixedTimeProvider(new DateTimeOffset(2026, 8, 13, 1, 0, 0, TimeSpan.Zero)));
            var project = new ProjectBindingContext("Project", new string('c', 64), "project-identity/1");
            var bootstrap = await application.BootstrapAsync(
                new BootstrapRequest(MutationRequestId.New(), "ru-RU", "Nova", "rpg_engineering", true, true),
                cancellationToken);
            var start = await application.StartQuestAsync(
                new StartQuestRequest(MutationRequestId.New(), bootstrap.Hero.HeroId, "coding", "Локализация", "Проверить сохранённую локаль квеста."),
                project,
                cancellationToken);
            await application.ConfigureAsync(new ConfigureRequest("en-US", "rpg_engineering", true, true), cancellationToken);

            var endpoint = new HeroPassportMcpEndpoint(application, project);
            var result = await endpoint.FinishQuestAsync(
                MutationRequestId.New().ToString(),
                start.Quest.QuestId.ToString(),
                "success",
                "Завершена проверка локализации с сохранением локали квеста и наблюдаемыми тестами.",
                new McpFinishMetricsInput(true, 0, 0, "passed", "observed", "passed", "observed"),
                ["coding", "testing_awareness", "scope_control"],
                cancellationToken);

            Assert.Equal("ru-RU", (await application.GetHeroCardAsync(bootstrap.Hero.HeroId, project, cancellationToken)).Hero is not null ? start.Quest.Locale : string.Empty);
            Assert.Equal("✓ Квест завершён · +95 XP", result.DisplayText);
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    private static string CreateDatabasePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "hero-passport-localization-tests", Guid.NewGuid().ToString("N"));
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
