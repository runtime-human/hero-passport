using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HeroPassport.Infrastructure.Persistence;

public sealed class HeroPassportDbContextFactory(string databasePath) : IDbContextFactory<HeroPassportDbContext>
{
    private readonly string _connectionString = HeroPassportDatabase.CreateConnectionString(databasePath);
    private readonly SqliteConnectionPolicyInterceptor _connectionInterceptor = new();

    public HeroPassportDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HeroPassportDbContext>()
            .UseSqlite(_connectionString)
            .AddInterceptors(_connectionInterceptor)
            .Options;

        return new HeroPassportDbContext(options);
    }
}
