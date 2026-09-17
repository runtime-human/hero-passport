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

    [Fact]
    public async Task ProjectHistoryReturnsNewestTwentyFiveAcrossHeroesAndIncludesOpenQuest()
    {
        var token = TestContext.Current.CancellationToken;
        var path = CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            var app = CreateApplication(path);
            var primary = (await app.BootstrapAsync(
                new BootstrapRequest(MutationRequestId.New(), "en-US", "Nova", "rpg_engineering", true, true),
                token)).Hero;
            var secondary = (await app.CreateHeroAsync(
                new CreateHeroRequest(MutationRequestId.New(), "Bolt"),
                token)).Hero;
            var currentProject = new ProjectBindingContext(
                "Current Project",
                new string('b', 64),
                "project-identity/1");
            var foreignProject = new ProjectBindingContext(
                "Foreign Project",
                new string('c', 64),
                "project-identity/1");

            var foreignQuest = (await app.StartQuestAsync(
                new StartQuestRequest(
                    MutationRequestId.New(),
                    primary.HeroId,
                    "coding",
                    "Foreign Quest",
                    "This Quest must never appear in current Project history."),
                foreignProject,
                token)).Quest;
            await app.FinishQuestAsync(
                CleanFinish(MutationRequestId.New(), foreignQuest.QuestId, ["coding"]),
                foreignProject,
                token);

            for (var index = 0; index < 25; index++)
            {
                var hero = index % 2 == 0 ? primary : secondary;
                var quest = (await app.StartQuestAsync(
                    new StartQuestRequest(
                        MutationRequestId.New(),
                        hero.HeroId,
                        "coding",
                        $"Quest {index:D2}",
                        $"Canonical bounded history fixture {index:D2}."),
                    currentProject,
                    token)).Quest;
                await app.FinishQuestAsync(
                    CleanFinish(MutationRequestId.New(), quest.QuestId, ["coding"]),
                    currentProject,
                    token);
            }

            var openQuest = (await app.StartQuestAsync(
                new StartQuestRequest(
                    MutationRequestId.New(),
                    secondary.HeroId,
                    "review",
                    "Open Quest 25",
                    "Remain open so history proves nullable finish fields."),
                currentProject,
                token)).Quest;

            var history = await app.GetProjectQuestHistoryAsync(currentProject, token);

            Assert.Equal("Current Project", history.ProjectDisplayName);
            Assert.Equal(25, history.Items.Count);
            Assert.Equal(openQuest.QuestId, history.Items[0].QuestId);
            Assert.Equal("Open Quest 25", history.Items[0].Title);
            Assert.Equal("Bolt", history.Items[0].HeroName);
            Assert.Equal("open", history.Items[0].Status);
            Assert.Null(history.Items[0].Result);
            Assert.Null(history.Items[0].XpGained);
            Assert.Null(history.Items[0].FinishedAtUtc);
            Assert.DoesNotContain(history.Items, item => item.Title == "Quest 00");
            Assert.Contains(history.Items, item => item.Title == "Quest 24" && item.HeroName == "Nova");
            Assert.Contains(history.Items, item => item.HeroName == "Bolt");
            Assert.DoesNotContain(history.Items, item => item.Title == "Foreign Quest");
            Assert.True(history.Items.Zip(history.Items.Skip(1), static (newer, older) => newer.StartedAtUtc >= older.StartedAtUtc).All(static ordered => ordered));
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task QuestHistoryDetailMapsCanonicalReportSkillsAndProjectScope()
    {
        var token = TestContext.Current.CancellationToken;
        var path = CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            var app = CreateApplication(path);
            var hero = (await app.BootstrapAsync(
                new BootstrapRequest(MutationRequestId.New(), "en-US", "Nova", "rpg_engineering", true, true),
                token)).Hero;
            var currentProject = new ProjectBindingContext(
                "Detail Project",
                new string('d', 64),
                "project-identity/1");
            var foreignProject = new ProjectBindingContext(
                "Foreign Detail Project",
                new string('e', 64),
                "project-identity/1");

            var finishedQuest = (await app.StartQuestAsync(
                new StartQuestRequest(
                    MutationRequestId.New(),
                    hero.HeroId,
                    "testing_awareness",
                    "Qualify history detail",
                    "Persist a canonical finished Quest report for bounded history detail."),
                currentProject,
                token)).Quest;
            var finish = new FinishQuestRequest(
                MutationRequestId.New(),
                finishedQuest.QuestId,
                "partial",
                "History detail keeps the persisted bounded report and three ordered Skills.",
                new FinishQuestMetrics(
                    TestsMentioned: true,
                    ScopeViolations: 1,
                    UserCorrections: 2,
                    BuildStatus: "passed",
                    BuildEvidence: "observed",
                    TestsStatus: "failed",
                    TestsEvidence: "observed"),
                ["testing_awareness", "review", "scope_control"]);
            var finishResult = await app.FinishQuestAsync(finish, currentProject, token);

            var openQuest = (await app.StartQuestAsync(
                new StartQuestRequest(
                    MutationRequestId.New(),
                    hero.HeroId,
                    "review",
                    "Open detail Quest",
                    "Open history detail must not synthesize a report."),
                currentProject,
                token)).Quest;

            var foreignQuest = (await app.StartQuestAsync(
                new StartQuestRequest(
                    MutationRequestId.New(),
                    hero.HeroId,
                    "coding",
                    "Foreign detail Quest",
                    "The current Project must not be able to observe this Quest."),
                foreignProject,
                token)).Quest;

            var detail = await app.GetQuestHistoryDetailAsync(finishedQuest.QuestId, currentProject, token);
            var openDetail = await app.GetQuestHistoryDetailAsync(openQuest.QuestId, currentProject, token);
            var foreignDetail = await app.GetQuestHistoryDetailAsync(foreignQuest.QuestId, currentProject, token);
            var missingDetail = await app.GetQuestHistoryDetailAsync(QuestId.New(), currentProject, token);

            Assert.NotNull(detail);
            Assert.Equal(finishedQuest.QuestId, detail.QuestId);
            Assert.Equal("Nova", detail.HeroName);
            Assert.Equal("Detail Project", detail.ProjectDisplayName);
            Assert.Equal("testing_awareness", detail.QuestType);
            Assert.Equal("Qualify history detail", detail.Title);
            Assert.Equal("Persist a canonical finished Quest report for bounded history detail.", detail.Goal);
            Assert.Equal("finished", detail.Status);
            Assert.NotNull(detail.FinishedAtUtc);
            Assert.NotNull(detail.Report);
            Assert.Equal("partial", detail.Report.Result);
            Assert.Equal("History detail keeps the persisted bounded report and three ordered Skills.", detail.Report.Summary);
            Assert.True(detail.Report.TestsMentioned);
            Assert.Equal(1, detail.Report.ScopeViolations);
            Assert.Equal(2, detail.Report.UserCorrections);
            Assert.Equal("passed", detail.Report.BuildStatus);
            Assert.Equal("observed", detail.Report.BuildEvidence);
            Assert.Equal("failed", detail.Report.TestsStatus);
            Assert.Equal("observed", detail.Report.TestsEvidence);
            Assert.Equal(finishResult.Reward.XpGained, detail.Report.XpGained);
            Assert.Equal(["testing_awareness", "review", "scope_control"], detail.Report.SkillsUsed);

            Assert.NotNull(openDetail);
            Assert.Equal("open", openDetail.Status);
            Assert.Null(openDetail.FinishedAtUtc);
            Assert.Null(openDetail.Report);
            Assert.Null(foreignDetail);
            Assert.Null(missingDetail);
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task RepeatedHistoryReadsDoNotCommitAnyDurableStateChange()
    {
        var token = TestContext.Current.CancellationToken;
        var path = CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            var app = CreateApplication(path);
            var hero = (await app.BootstrapAsync(
                new BootstrapRequest(MutationRequestId.New(), "en-US", "Nova", "rpg_engineering", true, true),
                token)).Hero;
            var project = new ProjectBindingContext(
                "Read Only Project",
                new string('f', 64),
                "project-identity/1");
            var finishedQuest = (await app.StartQuestAsync(
                new StartQuestRequest(
                    MutationRequestId.New(),
                    hero.HeroId,
                    "documentation",
                    "Read-only history",
                    "Prove history reads never commit a durable storage mutation."),
                project,
                token)).Quest;
            await app.FinishQuestAsync(
                CleanFinish(MutationRequestId.New(), finishedQuest.QuestId, ["documentation"]),
                project,
                token);

            await using var witness = await HeroPassportDatabase.OpenConnectionAsync(path, token);
            var beforeDataVersion = await DataVersionAsync(witness, token);
            var beforeCounts = await ProductTableCountsAsync(witness, token);

            for (var attempt = 0; attempt < 3; attempt++)
            {
                _ = await app.GetProjectQuestHistoryAsync(project, token);
                _ = await app.GetQuestHistoryDetailAsync(finishedQuest.QuestId, project, token);
                _ = await app.GetQuestHistoryDetailAsync(QuestId.New(), project, token);
            }

            Assert.Equal(beforeDataVersion, await DataVersionAsync(witness, token));
            Assert.Equal(beforeCounts, await ProductTableCountsAsync(witness, token));
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    private static HeroPassportApplication CreateApplication(string path) =>
        new(new SqliteHeroPassportStateStore(path), new IncrementingTimeProvider());

    private static FinishQuestRequest CleanFinish(
        MutationRequestId requestId,
        QuestId questId,
        IReadOnlyList<string> skills) =>
        new(
            requestId,
            questId,
            "success",
            "Completed the bounded Quest with directly observed passing build and tests.",
            new FinishQuestMetrics(
                TestsMentioned: true,
                ScopeViolations: 0,
                UserCorrections: 0,
                BuildStatus: "passed",
                BuildEvidence: "observed",
                TestsStatus: "passed",
                TestsEvidence: "observed"),
            skills);

    private static async Task<long> CountAsync(string path, string table, CancellationToken token)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(path, token);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table};";
        return Convert.ToInt64(await command.ExecuteScalarAsync(token), CultureInfo.InvariantCulture);
    }

    private static async Task<long> DataVersionAsync(SqliteConnection connection, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA data_version;";
        return Convert.ToInt64(await command.ExecuteScalarAsync(token), CultureInfo.InvariantCulture);
    }

    private static async Task<string> ProductTableCountsAsync(SqliteConnection connection, CancellationToken token)
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

        var snapshots = new List<string>(tables.Count);
        foreach (var table in tables)
        {
            await using var countCommand = connection.CreateCommand();
            countCommand.CommandText = $"SELECT COUNT(*) FROM \"{table.Replace("\"", "\"\"", StringComparison.Ordinal)}\";";
            var count = Convert.ToInt64(await countCommand.ExecuteScalarAsync(token), CultureInfo.InvariantCulture);
            snapshots.Add($"{table}:{count.ToString(CultureInfo.InvariantCulture)}");
        }

        return string.Join("|", snapshots);
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

    private sealed class IncrementingTimeProvider : TimeProvider
    {
        private long _ticks;

        public override DateTimeOffset GetUtcNow() =>
            new(2026, 9, 17, 0, 0, 0, TimeSpan.Zero).AddSeconds(Interlocked.Increment(ref _ticks));
    }
}
