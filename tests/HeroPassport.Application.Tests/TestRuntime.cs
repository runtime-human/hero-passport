using HeroPassport.Application.Runtime;
using HeroPassport.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace HeroPassport.Application.Tests;

internal static class TestRuntime
{
    public static string CreateDatabasePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "hero-passport-runtime-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "hero-passport.db");
    }

    public static HeroPassportApplication CreateApplication(string path) =>
        new(new SqliteHeroPassportStateStore(path), new FixedTimeProvider());

    public static void DeleteDatabase(string path)
    {
        SqliteConnection.ClearAllPools();
        var directory = Path.GetDirectoryName(path);
        if (directory is not null && Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);
    }
}
