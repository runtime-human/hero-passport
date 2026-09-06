using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using System.Data;

namespace HeroPassport.Infrastructure.Persistence;

public sealed partial class SqliteHeroPassportStateStore
{
    public async Task<BootstrapResult> BootstrapAsync(
        BootstrapStoreCommand command,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(_databasePath, cancellationToken).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable, deferred: false);
        var requestId = command.RequestId.ToString();
        var receipt = await ReceiptAsync(connection, transaction, "bootstrap", requestId, cancellationToken).ConfigureAwait(false);
        if (receipt is not null)
        {
            EnsureReceipt(receipt, command.ArgsEncodingVersion, command.ArgsHash);
            var settings = await SettingsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            var hero = await HeroAsync(connection, transaction, receipt.ResultEntityId ?? string.Empty, cancellationToken).ConfigureAwait(false);
            transaction.Commit();
            return new BootstrapResult(hero, settings.Snapshot(), true);
        }

        var current = await SettingsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        if (current.SetupCompleted)
        {
            throw new HeroPassportException("HP002", "Setup is already completed.");
        }

        var heroId = HeroId.New();
        var timestamp = Timestamp(now);
        await ExecuteAsync(
            connection,
            transaction,
            "INSERT INTO heroes(id,name,total_xp,trust,strain,success_streak,archived_at_utc,created_at_utc,updated_at_utc) VALUES($id,$name,0,50,20,0,NULL,$time,$time);",
            cancellationToken,
            ("$id", heroId.ToString()),
            ("$name", command.HeroName),
            ("$time", timestamp)).ConfigureAwait(false);
        await ExecuteAsync(
            connection,
            transaction,
            "UPDATE app_settings SET setup_completed=1,active_hero_id=$hero,locale=$locale,presentation_style=$style,auto_start_quest=$autoStart,auto_finish_quest=$autoFinish,config_version=config_version+1,updated_at_utc=$time WHERE id=1;",
            cancellationToken,
            ("$hero", heroId.ToString()),
            ("$locale", command.Locale),
            ("$style", command.PresentationStyle),
            ("$autoStart", command.AutoStartQuest ? 1 : 0),
            ("$autoFinish", command.AutoFinishQuest ? 1 : 0),
            ("$time", timestamp)).ConfigureAwait(false);
        await InsertReceiptAsync(
            connection,
            transaction,
            "bootstrap",
            requestId,
            command.ArgsEncodingVersion,
            command.ArgsHash,
            "bootstrap",
            heroId.ToString(),
            heroId.ToString(),
            timestamp,
            cancellationToken).ConfigureAwait(false);

        ObserveCommitBoundary("bootstrap", PersistenceCommitPhase.BeforeCommit);
        transaction.Commit();
        ObserveCommitBoundary("bootstrap", PersistenceCommitPhase.AfterCommit);
        return new BootstrapResult(
            new HeroIdentitySnapshot(heroId, command.HeroName),
            new SettingsSnapshot(command.Locale, command.PresentationStyle, command.AutoStartQuest, command.AutoFinishQuest),
            false);
    }

    public async Task<ConfigureResult> ConfigureAsync(
        ConfigureRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(_databasePath, cancellationToken).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable, deferred: false);
        var settings = await SettingsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        if (!settings.SetupCompleted)
        {
            throw new HeroPassportException("HP001", "Setup is required.");
        }

        var changed = !string.Equals(settings.Locale, request.Locale, StringComparison.Ordinal) ||
            !string.Equals(settings.PresentationStyle, request.PresentationStyle, StringComparison.Ordinal) ||
            settings.AutoStartQuest != request.AutoStartQuest ||
            settings.AutoFinishQuest != request.AutoFinishQuest;
        if (changed)
        {
            await ExecuteAsync(
                connection,
                transaction,
                "UPDATE app_settings SET locale=$locale,presentation_style=$style,auto_start_quest=$autoStart,auto_finish_quest=$autoFinish,config_version=config_version+1,updated_at_utc=$time WHERE id=1;",
                cancellationToken,
                ("$locale", request.Locale),
                ("$style", request.PresentationStyle),
                ("$autoStart", request.AutoStartQuest ? 1 : 0),
                ("$autoFinish", request.AutoFinishQuest ? 1 : 0),
                ("$time", Timestamp(now))).ConfigureAwait(false);
        }

        transaction.Commit();
        return new ConfigureResult(new SettingsSnapshot(request.Locale, request.PresentationStyle, request.AutoStartQuest, request.AutoFinishQuest), changed);
    }
}
