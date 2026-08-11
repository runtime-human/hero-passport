using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using Microsoft.Data.Sqlite;
using System.Data;
using System.Globalization;
using System.Security.Cryptography;

namespace HeroPassport.Infrastructure.Persistence;

public sealed class SqliteHeroPassportStateStore(string databasePath) : IHeroPassportStateStore
{
    private readonly string _databasePath = Path.GetFullPath(databasePath);

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
        var resultHero = InitialHeroSummary(heroId, command.HeroName);
        var resultSettings = new SettingsSnapshot(command.Locale, command.PresentationStyle, command.AutoStartQuest, command.AutoFinishQuest);
        return new BootstrapResult(true, resultHero, resultSettings, false);
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

        var projectId = await FindProjectIdAsync(connection, project.WorkspaceFingerprint, cancellationToken).ConfigureAwait(false);
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

    private static async Task<SettingsRow?> GetSettingsRowAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            transaction,
            "SELECT setup_completed,active_hero_id,locale,presentation_style,auto_start_quest,auto_finish_quest FROM app_settings WHERE id=1;");
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new SettingsRow(
            reader.GetInt64(0) != 0,
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetInt64(4) != 0,
            reader.GetInt64(5) != 0);
    }

    private static async Task<SettingsRow> GetSettingsRequiredAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken) =>
        await GetSettingsRowAsync(connection, transaction, cancellationToken).ConfigureAwait(false)
        ?? throw new HeroPassportException("HP900", "Application settings are unavailable.");

    private static async Task<ReceiptRow?> GetReceiptAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string operationKey,
        string requestId,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            transaction,
            "SELECT args_encoding_version,args_hash,result_entity_id,result_status FROM mutation_receipts WHERE operation_key=$operation AND request_id=$requestId;",
            ("$operation", operationKey),
            ("$requestId", requestId));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new ReceiptRow(
            reader.GetString(0),
            reader.GetFieldValue<byte[]>(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.GetString(3));
    }

    private static void EnsureReceiptMatches(ReceiptRow receipt, string encodingVersion, byte[] argsHash)
    {
        var matches =
            string.Equals(receipt.ArgsEncodingVersion, encodingVersion, StringComparison.Ordinal) &&
            receipt.ArgsHash.Length == argsHash.Length &&
            CryptographicOperations.FixedTimeEquals(receipt.ArgsHash, argsHash);
        if (!matches)
        {
            throw new HeroPassportException("HP135", "The mutation request ID was already used with different arguments.");
        }
    }

    private static async Task InsertReceiptAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string operationKey,
        string requestId,
        string argsEncodingVersion,
        byte[] argsHash,
        string resultKind,
        string resultEntityId,
        string? projectId,
        string? heroId,
        string timestamp,
        CancellationToken cancellationToken)
    {
        await ExecuteNonQueryAsync(
            connection,
            transaction,
            "INSERT INTO mutation_receipts(operation_key,request_id,args_encoding_version,args_hash,result_kind,result_entity_id,project_id,hero_id,result_status,effective_at_utc) VALUES($operation,$requestId,$encoding,$hash,$kind,$entityId,$projectId,$heroId,'active',$timestamp);",
            cancellationToken,
            ("$operation", operationKey),
            ("$requestId", requestId),
            ("$encoding", argsEncodingVersion),
            ("$hash", argsHash),
            ("$kind", resultKind),
            ("$entityId", resultEntityId),
            ("$projectId", projectId),
            ("$heroId", heroId),
            ("$timestamp", timestamp)).ConfigureAwait(false);
    }

    private static async Task<HeroSummary> GetHeroRequiredAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string? heroId,
        CancellationToken cancellationToken)
    {
        if (heroId is null)
        {
            throw new HeroPassportException("HP900", "Mutation receipt target is unavailable.");
        }

        return await FindHeroAsync(connection, transaction, heroId, cancellationToken).ConfigureAwait(false)
            ?? throw new HeroPassportException("HP140", "Hero was not found.");
    }

    private static async Task<HeroSummary?> FindHeroAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string heroId,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            transaction,
            "SELECT id,name,total_xp,trust,strain,archived_at_utc FROM heroes WHERE id=$id;",
            ("$id", heroId));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new HeroSummary(
            HeroId.Parse(reader.GetString(0)),
            reader.GetString(1),
            reader.GetInt64(2),
            1,
            "code_squire",
            reader.GetInt32(3),
            reader.GetInt32(4),
            !reader.IsDBNull(5));
    }

    private static async Task<string?> FindProjectIdAsync(
        SqliteConnection connection,
        string workspaceFingerprint,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            null,
            "SELECT id FROM projects WHERE workspace_fingerprint=$fingerprint;",
            ("$fingerprint", workspaceFingerprint));
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is null or DBNull ? null : Convert.ToString(result, CultureInfo.InvariantCulture);
    }

    private static async Task<IReadOnlyList<OpenQuestContext>> GetOpenQuestsAsync(
        SqliteConnection connection,
        string projectId,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            null,
            "SELECT q.id,q.hero_id,h.name,q.quest_type,q.title,q.goal,q.started_at_utc,q.locale FROM quest_sessions q JOIN heroes h ON h.id=q.hero_id WHERE q.project_id=$projectId AND q.status='open' ORDER BY q.started_at_utc ASC,q.id ASC;",
            ("$projectId", projectId));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var result = new List<OpenQuestContext>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            result.Add(new OpenQuestContext(
                QuestId.Parse(reader.GetString(0)),
                HeroId.Parse(reader.GetString(1)),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                ParseTimestamp(reader.GetString(6)),
                reader.GetString(7)));
        }

        return result;
    }

    private static SqliteCommand CreateCommand(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        }

        return command;
    }

    private static async Task ExecuteNonQueryAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = CreateCommand(connection, transaction, sql, parameters);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static HeroSummary InitialHeroSummary(HeroId heroId, string name) =>
        new(heroId, name, 0, 1, "code_squire", 50, 20, false);

    private static string FormatTimestamp(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseTimestamp(string value) =>
        DateTimeOffset.ParseExact(
            value,
            "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

    private sealed record SettingsRow(
        bool SetupCompleted,
        string? ActiveHeroId,
        string Locale,
        string PresentationStyle,
        bool AutoStartQuest,
        bool AutoFinishQuest)
    {
        public SettingsSnapshot ToSnapshot() =>
            new(Locale, PresentationStyle, AutoStartQuest, AutoFinishQuest);
    }

    private sealed record ReceiptRow(
        string ArgsEncodingVersion,
        byte[] ArgsHash,
        string? ResultEntityId,
        string ResultStatus);
}
