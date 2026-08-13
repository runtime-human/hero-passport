using Microsoft.EntityFrameworkCore;

namespace HeroPassport.Infrastructure.Persistence;

public sealed class HeroPassportDbContext(DbContextOptions<HeroPassportDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        HeroPassportStorageModel.Configure(modelBuilder);
    }
}
