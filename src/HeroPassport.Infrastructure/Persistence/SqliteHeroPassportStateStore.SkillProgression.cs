using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;

namespace HeroPassport.Infrastructure.Persistence;

public sealed partial class SqliteHeroPassportStateStore
{
    public Task<HeroSkillProgressionReadResult> GetSkillProgressionAsync(
        HeroId heroId,
        ProjectBindingContext project,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("0.2-F Skill progression read implementation is not complete.");
}
