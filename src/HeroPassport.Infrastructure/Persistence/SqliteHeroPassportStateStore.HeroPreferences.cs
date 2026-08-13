using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using System.Data;

namespace HeroPassport.Infrastructure.Persistence;

public sealed partial class SqliteHeroPassportStateStore
{
    public async Task<CreateHeroResult> CreateHeroAsync(CreateHeroStoreCommand command, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(_databasePath, cancellationToken).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable, deferred: false);
        var requestId = command.RequestId.ToString();
        var receipt = await ReceiptAsync(connection, transaction, "create_hero", requestId, cancellationToken).ConfigureAwait(false);
        if (receipt is not null)
        {
            EnsureReceipt(receipt, command.ArgsEncodingVersion, command.ArgsHash);
            var hero = await HeroAsync(connection, transaction, receipt.ResultEntityId ?? string.Empty, cancellationToken).ConfigureAwait(false);
            transaction.Commit();
            return new CreateHeroResult(hero, true);
        }

        var settings = await SettingsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        if (!settings.SetupCompleted)
        {
            throw new HeroPassportException("HP001", "Setup is required.");
        }

        var heroId = HeroId.New();
        var timestamp = Timestamp(now);
        await ExecuteAsync(connection, transaction,
            "INSERT INTO heroes(id,name,total_xp,trust,strain,success_streak,archived_at_utc,created_at_utc,updated_at_utc) VALUES($id,$name,0,50,20,0,NULL,$time,$time);",
            cancellationToken, ("$id", heroId.ToString()), ("$name", command.Name), ("$time", timestamp)).ConfigureAwait(false);
        await InsertReceiptAsync(connection, transaction, "create_hero", requestId, command.ArgsEncodingVersion, command.ArgsHash, "hero", heroId.ToString(), heroId.ToString(), timestamp, cancellationToken).ConfigureAwait(false);
        transaction.Commit();
        return new CreateHeroResult(new HeroIdentitySnapshot(heroId, command.Name), false);
    }

    public async Task ActivateHeroAsync(HeroId heroId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(_databasePath, cancellationToken).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable, deferred: false);
        var settings = await SettingsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        if (!settings.SetupCompleted)
        {
            throw new HeroPassportException("HP001", "Setup is required.");
        }

        _ = await HeroAsync(connection, transaction, heroId.ToString(), cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, transaction,
            "UPDATE app_settings SET active_hero_id=$hero,config_version=config_version+1,updated_at_utc=$time WHERE id=1;",
            cancellationToken, ("$hero", heroId.ToString()), ("$time", Timestamp(now))).ConfigureAwait(false);
        transaction.Commit();
    }
}
