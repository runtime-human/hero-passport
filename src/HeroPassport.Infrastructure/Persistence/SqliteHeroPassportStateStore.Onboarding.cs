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

        var receipt = await GetReceiptAsync(connection, transaction, "bootstrap", command.RequestId.ToString(), cancellationToken).ConfigureAwait(false);
        if (receipt is not null)
        {
            EnsureReceiptMatches(receipt, command.ArgsEncodingVersion, command.ArgsHash);
            var hero = await GetHeroRequiredAsync(connection, transaction, receipt.ResultEntityId, cancellationToken).ConfigureAwait(false);
            var settings = await GetSettingsRequiredAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            transaction.Commit();
            return new BootstrapResult(true, hero, settings.ToSnapshot(), true);
        }

        var currentSettings = await GetSettingsRowAsync(connection, transaction, cancellationToken).ConfigureAwait(false)
            ?? throw new HeroPassportException("HP900", "Application settings are unavailable.");
        if (currentSettings.SetupCompleted)
        {
            throw new HeroPassportException("HP002", "Setup is already completed.");
        }

        var heroId = HeroId.New();
        var timestamp = FormatTimestamp(now);
        await ExecuteNonQueryAsync(
            connection,
            transaction,
            "INSERT INTO heroes(id,name,total_xp,trust,strain,success_streak,created_at_utc,updated_at_utc) VALUES($id,$name,0,50,20,0,$created,$updated);",
            cancellationToken,
            ("$id", heroId.ToString()),
            ("$name", command.HeroName),
            ("$created", timestamp),
            ("$updated", timestamp)).ConfigureAwait(false);

        await ExecuteNonQueryAsync(
            connection,
            transaction,
            "UPDATE app_settings SET setup_completed=1, active_hero_id=$heroId, locale=$locale, presentation_style=$style, auto_start_quest=$autoStart, auto_finish_quest=$autoFinish, config_version=config_version+1, updated_at_utc=$updated WHERE id=1;",
            cancellationToken,
            ("$heroId", heroId.ToString()),
            ("$locale", command.Locale),
            ("$style", command.PresentationStyle),
            ("$autoStart", command.AutoStartQuest ? 1 : 0),
            ("$autoFinish", command.AutoFinishQuest ? 1 : 0),
            ("$updated", timestamp)).ConfigureAwait(false);

        await InsertReceiptAsync(
            connection,
            transaction,
            "bootstrap",
            command.RequestId.ToString(),
            command.ArgsEncodingVersion,
            command.ArgsHash,
            "bootstrap",
            heroId.ToString(),
            null,
            heroId.ToString(),
            timestamp,
            cancellationToken).ConfigureAwait(false);

        transaction.Commit();
        return new BootstrapResult(
            true,
            InitialHeroSummary(heroId, command.HeroName),
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
        var settings = await GetSettingsRowAsync(connection, transaction, cancellationToken).ConfigureAwait(false)
            ?? throw new HeroPassportException("HP900", "Application settings are unavailable.");
        if (!settings.SetupCompleted)
        {
            throw new HeroPassportException("HP001", "Setup is required.");
        }

        var changed =
            !string.Equals(settings.Locale, request.Locale, StringComparison.Ordinal) ||
            !string.Equals(settings.PresentationStyle, request.PresentationStyle, StringComparison.Ordinal) ||
            settings.AutoStartQuest != request.AutoStartQuest ||
            settings.AutoFinishQuest != request.AutoFinishQuest;

        if (changed)
        {
            await ExecuteNonQueryAsync(
                connection,
                transaction,
                "UPDATE app_settings SET locale=$locale, presentation_style=$style, auto_start_quest=$autoStart, auto_finish_quest=$autoFinish, config_version=config_version+1, updated_at_utc=$updated WHERE id=1;",
                cancellationToken,
                ("$locale", request.Locale),
                ("$style", request.PresentationStyle),
                ("$autoStart", request.AutoStartQuest ? 1 : 0),
                ("$autoFinish", request.AutoFinishQuest ? 1 : 0),
                ("$updated", FormatTimestamp(now))).ConfigureAwait(false);
        }

        transaction.Commit();
        return new ConfigureResult(
            new SettingsSnapshot(request.Locale, request.PresentationStyle, request.AutoStartQuest, request.AutoFinishQuest),
            changed);
    }

    public async Task<RuntimeContextResult> GetRuntimeContextAsync(
        ProjectBindingContext project,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(_databasePath, cancellationToken).ConfigureAwait(false);
        var settings = await GetSettingsRowAsync(connection, null, cancellationToken).ConfigureAwait(false)
            ?? throw new HeroPassportException("HP900", "Application settings are unavailable.");

        SettingsSnapshot? settingsSnapshot = null;
        HeroSummary? activeHero = null;
        if (settings.SetupCompleted)
        {
            settingsSnapshot = settings.ToSnapshot();
            if (settings.ActiveHeroId is not null)
            {
                activeHero = await GetHeroRequiredAsync(connection, null, settings.ActiveHeroId, cancellationToken).ConfigureAwait(false);
            }
        }

        var projectId = await FindProjectIdAsync(connection, null, project.WorkspaceFingerprint, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<OpenQuestContext> openQuests = projectId is null
            ? []
            : await GetOpenQuestsAsync(connection, projectId, cancellationToken).ConfigureAwait(false);

        return new RuntimeContextResult(
            HeroPassportVersions.ProductVersion,
            HeroPassportVersions.ContractVersion,
            HeroPassportVersions.SkillContractVersion,
            settings.SetupCompleted,
            settingsSnapshot,
            activeHero,
            new ProjectContextSnapshot(project.DisplayName),
            openQuests,
            HeroPassportVersions.CurrentRules);
    }

    public async Task<CreateHeroResult> CreateHeroAsync(
        CreateHeroStoreCommand command,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(_databasePath, cancellationToken).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable, deferred: false);
        var settings = await GetSettingsRowAsync(connection, transaction, cancellationToken).ConfigureAwait(false)
            ?? throw new HeroPassportException("HP900", "Application settings are unavailable.");
        if (!settings.SetupCompleted)
        {
            throw new HeroPassportException("HP001", "Setup is required.");
        }

        var receipt = await GetReceiptAsync(connection, transaction, "create_hero", command.RequestId.ToString(), cancellationToken).ConfigureAwait(false);
        if (receipt is not null)
        {
            EnsureReceiptMatches(receipt, command.ArgsEncodingVersion, command.ArgsHash);
            if (!string.Equals(receipt.ResultStatus, "active", StringComparison.Ordinal))
            {
                throw new HeroPassportException("HP140", "The previously created Hero no longer exists.");
            }

            var replayHero = await GetHeroRequiredAsync(connection, transaction, receipt.ResultEntityId, cancellationToken).ConfigureAwait(false);
            transaction.Commit();
            return new CreateHeroResult(replayHero, true);
        }

        var heroId = HeroId.New();
        var timestamp = FormatTimestamp(now);
        await ExecuteNonQueryAsync(
            connection,
            transaction,
            "INSERT INTO heroes(id,name,total_xp,trust,strain,success_streak,created_at_utc,updated_at_utc) VALUES($id,$name,0,50,20,0,$created,$updated);",
            cancellationToken,
            ("$id", heroId.ToString()),
            ("$name", command.Name),
            ("$created", timestamp),
            ("$updated", timestamp)).ConfigureAwait(false);

        await InsertReceiptAsync(
            connection,
            transaction,
            "create_hero",
            command.RequestId.ToString(),
            command.ArgsEncodingVersion,
            command.ArgsHash,
            "hero",
            heroId.ToString(),
            null,
            heroId.ToString(),
            timestamp,
            cancellationToken).ConfigureAwait(false);

        transaction.Commit();
        return new CreateHeroResult(InitialHeroSummary(heroId, command.Name), false);
    }

    public async Task ActivateHeroAsync(
        HeroId heroId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(_databasePath, cancellationToken).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable, deferred: false);
        var settings = await GetSettingsRowAsync(connection, transaction, cancellationToken).ConfigureAwait(false)
            ?? throw new HeroPassportException("HP900", "Application settings are unavailable.");
        if (!settings.SetupCompleted)
        {
            throw new HeroPassportException("HP001", "Setup is required.");
        }

        var hero = await FindHeroAsync(connection, transaction, heroId.ToString(), cancellationToken).ConfigureAwait(false)
            ?? throw new HeroPassportException("HP140", "Hero was not found.");
        if (hero.Archived)
        {
            throw new HeroPassportException("HP141", "Hero is archived.");
        }

        if (!string.Equals(settings.ActiveHeroId, heroId.ToString(), StringComparison.Ordinal))
        {
            await ExecuteNonQueryAsync(
                connection,
                transaction,
                "UPDATE app_settings SET active_hero_id=$heroId, config_version=config_version+1, updated_at_utc=$updated WHERE id=1;",
                cancellationToken,
                ("$heroId", heroId.ToString()),
                ("$updated", FormatTimestamp(now))).ConfigureAwait(false);
        }

        transaction.Commit();
    }
}
