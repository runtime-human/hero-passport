using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;

namespace HeroPassport.Infrastructure.Persistence;

public sealed partial class SqliteHeroPassportStateStore
{
    public async Task<string> GetQuestLocaleAsync(
        QuestId questId,
        ProjectBindingContext project,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(_databasePath, cancellationToken).ConfigureAwait(false);
        await using var command = CreateCommand(
            connection,
            null,
            """
            SELECT q.locale,p.workspace_fingerprint
            FROM quest_sessions q
            JOIN projects p ON p.id=q.project_id
            WHERE q.id=$questId;
            """,
            ("$questId", questId.ToString()));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new HeroPassportException("HP130", "Quest was not found.");
        }

        if (!string.Equals(reader.GetString(1), project.WorkspaceFingerprint, StringComparison.Ordinal))
        {
            throw new HeroPassportException("HP134", "Quest belongs to a different Project context.");
        }

        return reader.GetString(0);
    }
}
