using HeroPassport.Domain.Primitives;

namespace HeroPassport.Application.Runtime;

public sealed partial class HeroPassportApplication
{
    public Task<string> GetQuestLocaleAsync(
        QuestId questId,
        ProjectBindingContext project,
        CancellationToken cancellationToken = default) =>
        store.GetQuestLocaleAsync(questId, ValidateProject(project), cancellationToken);
}
