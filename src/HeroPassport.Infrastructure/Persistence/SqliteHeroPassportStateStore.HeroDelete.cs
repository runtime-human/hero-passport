using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using System.Data;
using System.Globalization;

namespace HeroPassport.Infrastructure.Persistence;

public sealed partial class SqliteHeroPassportStateStore
{
    public async Task DeleteHeroPermanentlyAsync(
        HeroId heroId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(_databasePath, cancellationToken).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable, deferred: false);

        var settings = await SettingsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        RequireSetup(settings);
        _ = await AdministrationHeroAsync(connection, transaction, heroId, cancellationToken).ConfigureAwait(false);

        var hero = heroId.ToString();
        if (string.Equals(settings.ActiveHeroId, hero, StringComparison.Ordinal))
        {
            throw new HeroPassportException("HP145", "The active Hero cannot be permanently deleted.");
        }

        await using (var openQuest = Command(
            connection,
            transaction,
            "SELECT EXISTS(SELECT 1 FROM quest_sessions WHERE hero_id=$hero AND status='open');",
            ("$hero", hero)))
        {
            var hasOpenQuest = Convert.ToInt64(
                await openQuest.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
                CultureInfo.InvariantCulture) != 0;
            if (hasOpenQuest)
            {
                throw new HeroPassportException("HP143", "Hero has an open Quest.");
            }
        }

        await ExecuteAsync(
            connection,
            transaction,
            "UPDATE mutation_receipts SET result_status='target_deleted' WHERE hero_id=$hero AND result_status='active';",
            cancellationToken,
            ("$hero", hero)).ConfigureAwait(false);

        await ExecuteAsync(
            connection,
            transaction,
            "DELETE FROM heroes WHERE id=$hero;",
            cancellationToken,
            ("$hero", hero)).ConfigureAwait(false);

        transaction.Commit();
    }
}
