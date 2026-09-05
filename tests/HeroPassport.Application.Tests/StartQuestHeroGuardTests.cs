using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using HeroPassport.Infrastructure.Persistence;
using Xunit;

namespace HeroPassport.Application.Tests;

public sealed class StartQuestHeroGuardTests
{
    [Fact]
    public async Task MissingAndArchivedHeroesAreRejectedBeforeProjectCreation()
    {
        var token = TestContext.Current.CancellationToken;
        var path = TestRuntime.CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);
            var app = TestRuntime.CreateApplication(path);
            await app.BootstrapAsync(
                new BootstrapRequest(MutationRequestId.New(), "en-US", "Nova", "rpg_engineering", true, true),
                token);
            var archived = (await app.CreateHeroAsync(new CreateHeroRequest(MutationRequestId.New(), "Archived"), token)).Hero;
            await ExecuteSqlAsync(
                path,
                "UPDATE heroes SET archived_at_utc='2026-08-13T12:00:00.000Z' WHERE id=$id;",
                token,
                ("$id", archived.HeroId.ToString()));

            var missing = await Assert.ThrowsAsync<HeroPassportException>(() =>
                app.StartQuestAsync(
                    new StartQuestRequest(
                        MutationRequestId.New(),
                        HeroId.Parse("01900000-0000-7000-8000-000000000998"),
                        "coding",
                        "Missing Hero",
                        "Reject a missing explicit Hero"),
                    Project('6', "Missing Project"),
                    token));
            Assert.Equal("HP140", missing.Code);

            var archivedError = await Assert.ThrowsAsync<HeroPassportException>(() =>
                app.StartQuestAsync(
                    new StartQuestRequest(
                        MutationRequestId.New(),
                        archived.HeroId,
                        "coding",
                        "Archived Hero",
                        "Reject an archived explicit Hero"),
                    Project('7', "Archived Project"),
                    token));
            Assert.Equal("HP141", archivedError.Code);

            Assert.Equal(0, await ScalarLongAsync(path, "SELECT COUNT(*) FROM projects;", token));
            Assert.Equal(0, await ScalarLongAsync(path, "SELECT COUNT(*) FROM quest_sessions;", token));
        }
        finally
        {
            TestRuntime.DeleteDatabase(path);
        }
    }

    private static ProjectBindingContext Project(char fingerprintCharacter, string displayName) =>
        new(displayName, new string(fingerprintCharacter, 64), "project-identity/1");

    private static async Task ExecuteSqlAsync(
        string path,
        string sql,
        CancellationToken token,
        params (string Name, object Value)[] parameters)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(path, token);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        }

        await command.ExecuteNonQueryAsync(token);
    }

    private static async Task<long> ScalarLongAsync(string path, string sql, CancellationToken token)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(path, token);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync(token), System.Globalization.CultureInfo.InvariantCulture);
    }
}
