using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using HeroPassport.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Xunit;

namespace HeroPassport.Application.Tests;

public sealed class BootstrapBehaviorTests
{
    [Fact]
    public async Task BootstrapReplaysSameIntentAndRejectsChangedOrFreshIntentAfterSetup()
    {
        var token = TestContext.Current.CancellationToken;
        var path = TestRuntime.CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            var app = TestRuntime.CreateApplication(path);
            var request = new BootstrapRequest(MutationRequestId.New(), "ru-RU", "Nova", "rpg_engineering", true, true);

            var first = await app.BootstrapAsync(request, token);
            var replay = await app.BootstrapAsync(request, token);

            Assert.False(first.Replayed);
            Assert.True(replay.Replayed);
            Assert.Equal(first.Hero, replay.Hero);

            var changed = await Assert.ThrowsAsync<HeroPassportException>(() =>
                app.BootstrapAsync(request with { HeroName = "Other" }, token));
            Assert.Equal("HP135", changed.Code);

            var fresh = await Assert.ThrowsAsync<HeroPassportException>(() =>
                app.BootstrapAsync(request with { BootstrapRequestId = MutationRequestId.New() }, token));
            Assert.Equal("HP002", fresh.Code);
        }
        finally
        {
            TestRuntime.DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task InvalidBootstrapHeroNameUsesStableInvalidRequestError()
    {
        var token = TestContext.Current.CancellationToken;
        var path = TestRuntime.CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            var app = TestRuntime.CreateApplication(path);

            var error = await Assert.ThrowsAsync<HeroPassportException>(() =>
                app.BootstrapAsync(new BootstrapRequest(MutationRequestId.New(), "en-US", null!, "minimal", true, true), token));

            Assert.Equal("HP100", error.Code);
        }
        finally
        {
            TestRuntime.DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task InjectedFailureBeforeCommitLeavesNoPartialBootstrapState()
    {
        var token = TestContext.Current.CancellationToken;
        var path = TestRuntime.CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            await ExecuteSqlAsync(
                path,
                """
                CREATE TRIGGER fail_bootstrap_setup
                BEFORE UPDATE OF setup_completed ON app_settings
                WHEN NEW.setup_completed = 1
                BEGIN
                    SELECT RAISE(ABORT, 'bootstrap-test-failure');
                END;
                """,
                token);

            var app = TestRuntime.CreateApplication(path);
            var request = new BootstrapRequest(MutationRequestId.New(), "en-US", "Nova", "rpg_engineering", true, true);

            await Assert.ThrowsAsync<SqliteException>(() => app.BootstrapAsync(request, token));

            Assert.Equal(0, await ScalarLongAsync(path, "SELECT COUNT(*) FROM heroes;", token));
            Assert.Equal(0, await ScalarLongAsync(path, "SELECT COUNT(*) FROM mutation_receipts;", token));
            Assert.Equal(0, await ScalarLongAsync(path, "SELECT setup_completed FROM app_settings WHERE id=1;", token));
            Assert.Equal(1, await ScalarLongAsync(path, "SELECT config_version FROM app_settings WHERE id=1;", token));
        }
        finally
        {
            TestRuntime.DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task FreshApplicationAfterCommittedBootstrapReplaysSameHero()
    {
        var token = TestContext.Current.CancellationToken;
        var path = TestRuntime.CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            var request = new BootstrapRequest(MutationRequestId.New(), "en-US", "Nova", "rpg_engineering", true, true);

            var committed = await TestRuntime.CreateApplication(path).BootstrapAsync(request, token);
            var replay = await TestRuntime.CreateApplication(path).BootstrapAsync(request, token);

            Assert.False(committed.Replayed);
            Assert.True(replay.Replayed);
            Assert.Equal(committed.Hero, replay.Hero);
            Assert.Equal(1, await ScalarLongAsync(path, "SELECT COUNT(*) FROM heroes;", token));
            Assert.Equal(1, await ScalarLongAsync(path, "SELECT COUNT(*) FROM mutation_receipts WHERE operation_key='bootstrap';", token));
        }
        finally
        {
            TestRuntime.DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task ConcurrentFreshBootstrapsCreateExactlyOneInitialHero()
    {
        var token = TestContext.Current.CancellationToken;
        var path = TestRuntime.CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            var first = TestRuntime.CreateApplication(path);
            var second = TestRuntime.CreateApplication(path);

            var results = await Task.WhenAll(
                CaptureAsync(() => first.BootstrapAsync(new BootstrapRequest(MutationRequestId.New(), "en-US", "One", "rpg_engineering", true, true), token)),
                CaptureAsync(() => second.BootstrapAsync(new BootstrapRequest(MutationRequestId.New(), "en-US", "Two", "rpg_engineering", true, true), token)));

            Assert.Single(results, static item => item.Result is not null);
            Assert.Equal("HP002", Assert.IsType<HeroPassportException>(Assert.Single(results, static item => item.Error is not null).Error).Code);
            Assert.Equal(1, await ScalarLongAsync(path, "SELECT COUNT(*) FROM heroes;", token));
        }
        finally
        {
            TestRuntime.DeleteDatabase(path);
        }
    }

    private static async Task<(BootstrapResult? Result, Exception? Error)> CaptureAsync(Func<Task<BootstrapResult>> action)
    {
        try { return (await action(), null); }
        catch (Exception exception) { return (null, exception); }
    }

    private static async Task ExecuteSqlAsync(string path, string sql, CancellationToken token)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(path, token);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(token);
    }

    private static async Task<long> ScalarLongAsync(string path, string sql, CancellationToken token)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(path, token);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (long)(await command.ExecuteScalarAsync(token) ?? 0L);
    }
}
