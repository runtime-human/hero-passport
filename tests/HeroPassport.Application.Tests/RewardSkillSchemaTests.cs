using HeroPassport.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using System.Globalization;
using Xunit;

namespace HeroPassport.Application.Tests;

public sealed class RewardSkillSchemaTests
{
    [Fact]
    public async Task RewardSkillSchemaRequiresCanonicalReportSkillKeys()
    {
        var token = TestContext.Current.CancellationToken;
        var path = TestRuntime.CreateDatabasePath();
        try
        {
            await HeroPassportDatabase.InitializeAsync(path, token);

            Assert.Equal(10, await ScalarLongAsync(path, "SELECT COUNT(*) FROM skills;", token));
            Assert.Equal(
                1,
                await ScalarLongAsync(
                    path,
                    "SELECT \"notnull\" FROM pragma_table_info('quest_report_skills') WHERE name='skill_key';",
                    token));
            Assert.Equal(
                1,
                await ScalarLongAsync(
                    path,
                    "SELECT COUNT(*) FROM pragma_foreign_key_list('quest_report_skills') WHERE \"table\"='skills' AND \"from\"='skill_key' AND \"to\"='skill_key';",
                    token));
        }
        finally
        {
            TestRuntime.DeleteDatabase(path);
        }
    }

    private static async Task<long> ScalarLongAsync(string path, string sql, CancellationToken token)
    {
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false,
        }.ToString());
        await connection.OpenAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync(token), CultureInfo.InvariantCulture);
    }
}
