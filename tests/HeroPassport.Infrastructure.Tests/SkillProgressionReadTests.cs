using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Engine;
using HeroPassport.Domain.Primitives;
using HeroPassport.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using System.Globalization;
using System.Text;
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
            var projectsBefore = await CountAsync(path, "projects", token);

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
            Assert.Equal(projectsBefore, await CountAsync(path, "projects", token));
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task SkillProgressionSeparatesHeroTotalsFromProjectContributionAndOtherHeroes()
    {
        var token = TestContext.Current.CancellationToken;
        var path = CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            var app = CreateApplication(path);
            var nova = (await app.BootstrapAsync(
                new BootstrapRequest(
                    MutationRequestId.New(),
                    "en-US",
                    "Nova",
                    "rpg_engineering",
                    true,
                    true),
                token)).Hero;
            var bolt = (await app.CreateHeroAsync(
                new CreateHeroRequest(MutationRequestId.New(), "Bolt"),
                token)).Hero;
            var projectA = new ProjectBindingContext(
                "Project A",
                new string('b', 64),
                "project-identity/1");
            var projectB = new ProjectBindingContext(
                "Project B",
                new string('c', 64),
                "project-identity/1");

            await FinishQuestAsync(
                app,
                nova.HeroId,
                projectA,
                "Nova A",
                ["coding", "testing_awareness"],
                token);
            await FinishQuestAsync(
                app,
                nova.HeroId,
                projectB,
                "Nova B",
                ["coding"],
                token);
            await FinishQuestAsync(
                app,
                bolt.HeroId,
                projectA,
                "Bolt A",
                ["coding"],
                token);

            var result = await app.GetSkillProgressionAsync(nova.HeroId, projectA, token);

            Assert.Equal("Nova", result.HeroName);
            Assert.Equal("Project A", result.ProjectDisplayName);
            Assert.Equal(CanonicalSkillOrder, result.Skills.Select(static skill => skill.SkillKey));

            var coding = Assert.Single(result.Skills, static skill => skill.SkillKey == "coding");
            Assert.Equal(152, coding.Hero.Xp);
            Assert.Equal(57, coding.ProjectContribution.Xp);
            AssertHeroProgressMatchesDomain(coding.Hero);

            var testing = Assert.Single(
                result.Skills,
                static skill => skill.SkillKey == "testing_awareness");
            Assert.Equal(38, testing.Hero.Xp);
            Assert.Equal(38, testing.ProjectContribution.Xp);
            AssertHeroProgressMatchesDomain(testing.Hero);

            var review = Assert.Single(result.Skills, static skill => skill.SkillKey == "review");
            AssertZeroProgress(review.Hero);
            Assert.Equal(0, review.ProjectContribution.Xp);
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task SkillProgressionRejectsUnknownHero()
    {
        var token = TestContext.Current.CancellationToken;
        var path = CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            var app = CreateApplication(path);
            _ = await app.BootstrapAsync(
                new BootstrapRequest(
                    MutationRequestId.New(),
                    "en-US",
                    "Nova",
                    "rpg_engineering",
                    true,
                    true),
                token);
            var project = new ProjectBindingContext(
                "Unknown Hero Project",
                new string('d', 64),
                "project-identity/1");

            var error = await Assert.ThrowsAsync<HeroPassportException>(() =>
                app.GetSkillProgressionAsync(HeroId.New(), project, token));

            Assert.Equal("HP140", error.Code);
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task RepeatedSkillProgressionReadsDoNotChangeAnyProductRow()
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
                "Read Only Skill Project",
                new string('e', 64),
                "project-identity/1");
            await FinishQuestAsync(
                app,
                hero.HeroId,
                project,
                "Read-only Skill Quest",
                ["coding", "scope_control"],
                token);

            await using var witness = await HeroPassportDatabase.OpenConnectionAsync(path, token);
            var before = await ProductTableSnapshotAsync(witness, token);

            for (var attempt = 0; attempt < 3; attempt++)
            {
                _ = await app.GetSkillProgressionAsync(hero.HeroId, project, token);
            }

            var after = await ProductTableSnapshotAsync(witness, token);
            Assert.Equal(before, after);
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    private static async Task FinishQuestAsync(
        HeroPassportApplication app,
        HeroId heroId,
        ProjectBindingContext project,
        string title,
        IReadOnlyList<string> skills,
        CancellationToken token)
    {
        var quest = (await app.StartQuestAsync(
            new StartQuestRequest(
                MutationRequestId.New(),
                heroId,
                "coding",
                title,
                "Create a deterministic Skill progression fixture."),
            project,
            token)).Quest;

        _ = await app.FinishQuestAsync(
            new FinishQuestRequest(
                MutationRequestId.New(),
                quest.QuestId,
                "success",
                "Completed the fixture with directly observed passing build and tests.",
                new FinishQuestMetrics(
                    TestsMentioned: true,
                    ScopeViolations: 0,
                    UserCorrections: 0,
                    BuildStatus: "passed",
                    BuildEvidence: "observed",
                    TestsStatus: "passed",
                    TestsEvidence: "observed"),
                skills),
            project,
            token);
    }

    private static void AssertHeroProgressMatchesDomain(SkillProgressionReadSnapshot skill)
    {
        var version = SkillProgressionRules.RuleVersion;
        var expectedLevel = SkillProgressionRules.Level(skill.Xp, version);
        Assert.Equal(expectedLevel, skill.Level);
        Assert.Equal(SkillProgressionRules.IsLevelCapped(expectedLevel, version), skill.IsLevelCapped);
        Assert.Equal(SkillProgressionRules.LevelXp(skill.Xp, expectedLevel, version), skill.LevelXp);
        Assert.Equal(
            SkillProgressionRules.NextLevelXpRequired(expectedLevel, version),
            skill.NextLevelXpRequired);
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

    private static async Task<long> CountAsync(
        string path,
        string table,
        CancellationToken token)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(path, token);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {QuoteIdentifier(table)};";
        return Convert.ToInt64(await command.ExecuteScalarAsync(token), CultureInfo.InvariantCulture);
    }

    private static async Task<string> ProductTableSnapshotAsync(
        SqliteConnection connection,
        CancellationToken token)
    {
        await using var tablesCommand = connection.CreateCommand();
        tablesCommand.CommandText = """
            SELECT name
            FROM sqlite_master
            WHERE type='table'
              AND name NOT LIKE 'sqlite_%'
              AND name NOT LIKE '__EF%'
            ORDER BY name;
            """;
        var tables = new List<string>();
        await using (var reader = await tablesCommand.ExecuteReaderAsync(token))
        {
            while (await reader.ReadAsync(token))
            {
                tables.Add(reader.GetString(0));
            }
        }

        var snapshot = new StringBuilder();
        foreach (var table in tables)
        {
            var columns = await ColumnNamesAsync(connection, table, token);
            snapshot.Append('[').Append(table).Append(']').AppendLine();

            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT {string.Join(",", columns.Select(QuoteIdentifier))} FROM {QuoteIdentifier(table)} ORDER BY rowid;";
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
            {
                for (var index = 0; index < reader.FieldCount; index++)
                {
                    if (index > 0)
                    {
                        snapshot.Append('|');
                    }

                    AppendValue(snapshot, reader.GetValue(index));
                }

                snapshot.AppendLine();
            }
        }

        return snapshot.ToString();
    }

    private static async Task<string[]> ColumnNamesAsync(
        SqliteConnection connection,
        string table,
        CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({QuoteIdentifier(table)});";
        await using var reader = await command.ExecuteReaderAsync(token);
        var columns = new List<string>();
        while (await reader.ReadAsync(token))
        {
            columns.Add(reader.GetString(1));
        }

        return columns.ToArray();
    }

    private static void AppendValue(StringBuilder builder, object value)
    {
        switch (value)
        {
            case DBNull:
                builder.Append("N:");
                break;
            case byte[] bytes:
                builder.Append("B:").Append(Convert.ToHexString(bytes));
                break;
            case string text:
                builder.Append("T:")
                    .Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(text)));
                break;
            case long integer:
                builder.Append("I:")
                    .Append(integer.ToString(CultureInfo.InvariantCulture));
                break;
            case double real:
                builder.Append("R:")
                    .Append(real.ToString("R", CultureInfo.InvariantCulture));
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported SQLite snapshot value type '{value.GetType().FullName}'.");
        }
    }

    private static string QuoteIdentifier(string identifier) =>
        $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

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
