using Microsoft.EntityFrameworkCore;

namespace HeroPassport.Infrastructure.Persistence;

public sealed class HeroPassportDbContext(DbContextOptions<HeroPassportDbContext> options) : DbContext(options)
{
}
