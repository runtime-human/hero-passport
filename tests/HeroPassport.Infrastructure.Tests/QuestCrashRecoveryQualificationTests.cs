using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using HeroPassport.Infrastructure.Persistence;
using System.Diagnostics;
using Xunit;

namespace HeroPassport.Infrastructure.Tests;

public sealed class QuestCrashRecoveryQualificationTests
{
    private const string ProjectFingerprint = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
    private static readonly string[] CodingSkills = ["coding"];

    [Theory]
    [InlineData("before-commit", false)]
    [InlineData("after-commit", true)]
    public async Task StartChildProcessKillConvergesAtCommitBoundary(string crashPhase, bool committedBeforeKill)
    {
        var token = TestContext.Current.CancellationToken;
        var fixture = await CreateFixtureAsync(token);
        Process? child = null;

        try
        {
            var request = new StartQuestRequest(
                MutationRequestId.New(),
                fixture.HeroId,
                "coding",
                "Crash Start",
                "Qualify Start persistence recovery around COMMIT.");

            SqliteConnection.ClearAllPools();
            child = StartHarness(
                fixture.DatabasePath,
                "start",
                crashPhase,
                request.StartRequestId.ToString(),
                fixture.SignalPath,
                fixture.HeroId.ToString(),
                ProjectFingerprint);
            await WaitForFileAsync(fixture.SignalPath, child, token);

            child.Kill(entireProcessTree: true);
            await child.WaitForExitAsync(token);

            Assert.Equal(committedBeforeKill ? 1 : 0, await ScalarLongAsync(fixture.DatabasePath, "SELECT COUNT(*) FROM projects;", token));
            Assert.Equal(committedBeforeKill ? 1 : 0, await ScalarLongAsync(fixture.DatabasePath, "SELECT COUNT(*) FROM quest_sessions;", token));
            Assert.Equal(committedBeforeKill ? 1 : 0, await ScalarLongAsync(fixture.DatabasePath, "SELECT COUNT(*) FROM hero_project_stats;", token));
            Assert.Equal(committedBeforeKill ? 1 : 0, await ScalarLongAsync(fixture.DatabasePath, "SELECT COUNT(*) FROM mutation_receipts WHERE operation_key='start_quest';", token));
            await AssertIntegrityAsync(fixture.DatabasePath, token);

            var retry = await CreateApplication(fixture.DatabasePath).StartQuestAsync(request, Project(), token);
            Assert.Equal(committedBeforeKill, retry.Replayed);
            Assert.Equal(1, await ScalarLongAsync(fixture.DatabasePath, "SELECT COUNT(*) FROM projects;", token));
            Assert.Equal(1, await ScalarLongAsync(fixture.DatabasePath, "SELECT COUNT(*) FROM quest_sessions;", token));
            Assert.Equal(1, await ScalarLongAsync(fixture.DatabasePath, "SELECT COUNT(*) FROM hero_project_stats;", token));
            Assert.Equal(1, await ScalarLongAsync(fixture.DatabasePath, "SELECT COUNT(*) FROM mutation_receipts WHERE operation_key='start_quest';", token));
        }
        finally
        {
            await CleanupAsync(fixture.Directory, child);
        }
    }

    [Theory]
    [InlineData("before-commit", false)]
    [InlineData("after-commit", true)]
    public async Task FinishChildProcessKillConvergesAtCommitBoundary(string crashPhase, bool committedBeforeKill)
    {
        var token = TestContext.Current.CancellationToken;
        var fixture = await CreateFixtureAsync(token);
        Process? child = null;

        try
        {
            var application = CreateApplication(fixture.DatabasePath);
            var started = await application.StartQuestAsync(
                new StartQuestRequest(
                    MutationRequestId.New(),
                    fixture.HeroId,
                    "coding",
                    "Crash Finish",
                    "Prepare a durable Quest for Finish crash recovery qualification."),
                Project(),
                token);
            var request = FinishRequest(MutationRequestId.New(), started.Quest.QuestId);

            SqliteConnection.ClearAllPools();
            child = StartHarness(
                fixture.DatabasePath,
                "finish",
                crashPhase,
                request.FinishRequestId.ToString(),
                fixture.SignalPath,
                started.Quest.QuestId.ToString(),
                ProjectFingerprint);
            await WaitForFileAsync(fixture.SignalPath, child, token);

            child.Kill(entireProcessTree: true);
            await child.WaitForExitAsync(token);

            Assert.Equal(committedBeforeKill ? "finished" : "open", await ScalarStringAsync(fixture.DatabasePath, "SELECT status FROM quest_sessions;", token));
            Assert.Equal(committedBeforeKill ? 1 : 0, await ScalarLongAsync(fixture.DatabasePath, "SELECT COUNT(*) FROM quest_reports;", token));
            Assert.Equal(committedBeforeKill ? 3 : 0, await ScalarLongAsync(fixture.DatabasePath, "SELECT COUNT(*) FROM quest_reward_components;", token));
            Assert.Equal(committedBeforeKill ? 2 : 0, await ScalarLongAsync(fixture.DatabasePath, "SELECT COUNT(*) FROM quest_trust_strain_components;", token));
            Assert.Equal(committedBeforeKill ? 1 : 0, await ScalarLongAsync(fixture.DatabasePath, "SELECT COUNT(*) FROM quest_report_skills;", token));
            Assert.Equal(committedBeforeKill ? 1 : 0, await ScalarLongAsync(fixture.DatabasePath, "SELECT COUNT(*) FROM hero_skills;", token));
            Assert.Equal(committedBeforeKill ? 1 : 0, await ScalarLongAsync(fixture.DatabasePath, "SELECT COUNT(*) FROM xp_events;", token));
            Assert.Equal(committedBeforeKill ? 1 : 0, await ScalarLongAsync(fixture.DatabasePath, "SELECT COUNT(*) FROM mutation_receipts WHERE operation_key='finish_quest';", token));
            Assert.Equal(committedBeforeKill ? 85 : 0, await ScalarLongAsync(fixture.DatabasePath, "SELECT total_xp FROM heroes WHERE id='" + fixture.HeroId + "';", token));
            Assert.Equal(committedBeforeKill ? 52 : 50, await ScalarLongAsync(fixture.DatabasePath, "SELECT trust FROM heroes WHERE id='" + fixture.HeroId + "';", token));
            Assert.Equal(committedBeforeKill ? 18 : 20, await ScalarLongAsync(fixture.DatabasePath, "SELECT strain FROM heroes WHERE id='" + fixture.HeroId + "';", token));
            Assert.Equal(committedBeforeKill ? 1 : 0, await ScalarLongAsync(fixture.DatabasePath, "SELECT success_streak FROM heroes WHERE id='" + fixture.HeroId + "';", token));
            Assert.Equal(committedBeforeKill ? 85 : 0, await ScalarLongAsync(fixture.DatabasePath, "SELECT COALESCE(MAX(xp),0) FROM hero_skills WHERE hero_id='" + fixture.HeroId + "' AND skill_key='coding';", token));
            Assert.Equal(committedBeforeKill ? 1 : 0, await ScalarLongAsync(fixture.DatabasePath, "SELECT quests_finished FROM hero_project_stats;", token));
            Assert.Equal(committedBeforeKill ? 85 : 0, await ScalarLongAsync(fixture.DatabasePath, "SELECT total_xp_earned FROM hero_project_stats;", token));
            await AssertIntegrityAsync(fixture.DatabasePath, token);

            var retry = await CreateApplication(fixture.DatabasePath).FinishQuestAsync(request, Project(), token);
            Assert.Equal(committedBeforeKill, retry.Replayed);
            Assert.Equal(85, retry.Reward.XpGained);
            Assert.Equal(3, retry.Reward.Components.Count);
            Assert.Equal(50, retry.TrustStrain.TrustBefore);
            Assert.Equal(52, retry.TrustStrain.TrustAfter);
            Assert.Equal(20, retry.TrustStrain.StrainBefore);
            Assert.Equal(18, retry.TrustStrain.StrainAfter);
            Assert.Collection(
                retry.TrustStrain.Components,
                component => Assert.Equal(new TrustStrainComponentSnapshot("success_outcome", 1, -1), component),
                component => Assert.Equal(new TrustStrainComponentSnapshot("clean_success_bonus", 1, -1), component));
            Assert.Equal(0, retry.Streak.Before);
            Assert.Equal(1, retry.Streak.After);
            Assert.Equal("finished", await ScalarStringAsync(fixture.DatabasePath, "SELECT status FROM quest_sessions;", token));
            Assert.Equal(1, await ScalarLongAsync(fixture.DatabasePath, "SELECT COUNT(*) FROM quest_reports;", token));
            Assert.Equal(3, await ScalarLongAsync(fixture.DatabasePath, "SELECT COUNT(*) FROM quest_reward_components;", token));
            Assert.Equal(2, await ScalarLongAsync(fixture.DatabasePath, "SELECT COUNT(*) FROM quest_trust_strain_components;", token));
            Assert.Equal(1, await ScalarLongAsync(fixture.DatabasePath, "SELECT COUNT(*) FROM quest_report_skills;", token));
            Assert.Equal(1, await ScalarLongAsync(fixture.DatabasePath, "SELECT COUNT(*) FROM hero_skills;", token));
            Assert.Equal(1, await ScalarLongAsync(fixture.DatabasePath, "SELECT COUNT(*) FROM xp_events;", token));
            Assert.Equal(1, await ScalarLongAsync(fixture.DatabasePath, "SELECT COUNT(*) FROM mutation_receipts WHERE operation_key='finish_quest';", token));
            Assert.Equal(85, await ScalarLongAsync(fixture.DatabasePath, "SELECT total_xp FROM heroes WHERE id='" + fixture.HeroId + "';", token));
            Assert.Equal(52, await ScalarLongAsync(fixture.DatabasePath, "SELECT trust FROM heroes WHERE id='" + fixture.HeroId + "';", token));
            Assert.Equal(18, await ScalarLongAsync(fixture.DatabasePath, "SELECT strain FROM heroes WHERE id='" + fixture.HeroId + "';", token));
            Assert.Equal(1, await ScalarLongAsync(fixture.DatabasePath, "SELECT success_streak FROM heroes WHERE id='" + fixture.HeroId + "';", token));
            Assert.Equal(85, await ScalarLongAsync(fixture.DatabasePath, "SELECT xp FROM hero_skills WHERE hero_id='" + fixture.HeroId + "' AND skill_key='coding';", token));
            Assert.Equal(1, await ScalarLongAsync(fixture.DatabasePath, "SELECT quests_finished FROM hero_project_stats;", token));
            Assert.Equal(85, await ScalarLongAsync(fixture.DatabasePath, "SELECT total_xp_earned FROM hero_project_stats;", token));
        }
        finally
        {
            await CleanupAsync(fixture.Directory, child);
        }
    }

    private static async Task<CrashFixture> CreateFixtureAsync(CancellationToken token)
    {
        var directory = Path.Combine(Path.GetTempPath(), "HeroPassport.QuestCrashQualification", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "hero-passport.db");
        await HeroPassportDatabase.InitializeAsync(databasePath, token);
        var bootstrap = await CreateApplication(databasePath).BootstrapAsync(
            new BootstrapRequest(MutationRequestId.New(), "en-US", "Crash Nova", "rpg_engineering", true, true),
            token);
        return new CrashFixture(directory, databasePath, Path.Combine(directory, "commit-boundary.signal"), bootstrap.Hero.HeroId);
    }

    private static FinishQuestRequest FinishRequest(MutationRequestId requestId, QuestId questId) =>
        new(
            requestId,
            questId,
            "success",
            "Qualify Finish persistence recovery around COMMIT.",
            new FinishQuestMetrics(false, 0, 0, "not_run", "none", "not_run", "none"),
            CodingSkills);

    private static ProjectBindingContext Project() =>
        new("Crash Project", ProjectFingerprint, "project-identity/1");

    private static HeroPassportApplication CreateApplication(string path) =>
        new(new SqliteHeroPassportStateStore(path), TimeProvider.System);

    private static Process StartHarness(
        string databasePath,
        string operation,
        string crashPhase,
        string requestId,
        string signalPath,
        string entityId,
        string projectFingerprint)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add(CrashHarnessDll());
        start.ArgumentList.Add(operation);
        start.ArgumentList.Add(databasePath);
        start.ArgumentList.Add(crashPhase);
        start.ArgumentList.Add(requestId);
        start.ArgumentList.Add(signalPath);
        start.ArgumentList.Add(entityId);
        start.ArgumentList.Add(projectFingerprint);
        return Process.Start(start) ?? throw new Xunit.Sdk.XunitException("Crash harness process could not be started.");
    }

    private static async Task WaitForFileAsync(string path, Process child, CancellationToken token)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (File.Exists(path))
            {
                return;
            }

            if (child.HasExited)
            {
                throw new Xunit.Sdk.XunitException($"Crash harness exited before reaching the commit boundary (exit {child.ExitCode}).");
            }

            await Task.Delay(50, token);
        }

        throw new Xunit.Sdk.XunitException("Crash harness did not reach the commit boundary within 15 seconds.");
    }

    private static async Task AssertIntegrityAsync(string path, CancellationToken token)
    {
        Assert.Equal("ok", await ScalarStringAsync(path, "PRAGMA quick_check;", token));
        Assert.Equal(0, await RowCountAsync(path, "PRAGMA foreign_key_check;", token));
    }

    private static string CrashHarnessDll()
    {
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var path = Path.Combine(RepoRoot(), "tests", "HeroPassport.CrashHarness", "bin", configuration, "net10.0", "HeroPassport.CrashHarness.dll");
        Assert.True(File.Exists(path), $"Crash harness was not built at {path}.");
        return path;
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HeroPassport.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Hero Passport repository root was not found from the infrastructure test output directory.");
    }

    private static async Task<long> ScalarLongAsync(string path, string sql, CancellationToken token)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(path, token);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync(token), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<string> ScalarStringAsync(string path, string sql, CancellationToken token)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(path, token);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(await command.ExecuteScalarAsync(token), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static async Task<int> RowCountAsync(string path, string sql, CancellationToken token)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(path, token);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(token);
        var count = 0;
        while (await reader.ReadAsync(token))
        {
            count++;
        }

        return count;
    }

    private static async Task CleanupAsync(string directory, Process? child)
    {
        if (child is { HasExited: false })
        {
            child.Kill(entireProcessTree: true);
            await child.WaitForExitAsync(CancellationToken.None);
        }

        SqliteConnection.ClearAllPools();
        try { Directory.Delete(directory, recursive: true); } catch (IOException) { }
    }

    private sealed record CrashFixture(string Directory, string DatabasePath, string SignalPath, HeroId HeroId);
}
