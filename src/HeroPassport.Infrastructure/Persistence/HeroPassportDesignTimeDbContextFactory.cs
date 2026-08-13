using Microsoft.EntityFrameworkCore.Design;

namespace HeroPassport.Infrastructure.Persistence;

public sealed class HeroPassportDesignTimeDbContextFactory : IDesignTimeDbContextFactory<HeroPassportDbContext>
{
    public HeroPassportDbContext CreateDbContext(string[] args)
    {
        var databasePath = Path.Combine(Path.GetTempPath(), "hero-passport-design.db");
        return new HeroPassportDbContextFactory(databasePath).CreateDbContext();
    }
}
