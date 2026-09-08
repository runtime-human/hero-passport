using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;

namespace HeroPassport.Infrastructure.Persistence;

public sealed partial class SqliteHeroPassportStateStore
{
    public async Task<string> GetQuestLocaleAsync(
        QuestId questId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await HeroPassportDatabase
            .OpenConnectionAsync(_databasePath, cancellationToken)
            .ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT locale FROM quest_sessions WHERE id=$id;";
        command.Parameters.AddWithValue("$id", questId.ToString());

        var locale = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
        return locale ?? throw new HeroPassportException("HP130", "Quest was not found.");
    }
}
