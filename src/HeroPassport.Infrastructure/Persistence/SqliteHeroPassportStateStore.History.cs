using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;

namespace HeroPassport.Infrastructure.Persistence;

public sealed partial class SqliteHeroPassportStateStore
{
    public Task<ProjectQuestHistoryResult> GetProjectQuestHistoryAsync(
        ProjectBindingContext project,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Quest history reads are not implemented yet.");

    public Task<QuestHistoryDetailResult?> GetQuestHistoryDetailAsync(
        QuestId questId,
        ProjectBindingContext project,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Quest history reads are not implemented yet.");
}
