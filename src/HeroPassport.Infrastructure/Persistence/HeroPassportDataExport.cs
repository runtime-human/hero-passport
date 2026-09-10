using Microsoft.Data.Sqlite;
using System.Globalization;
using System.Text.Json;

namespace HeroPassport.Infrastructure.Persistence;

public sealed record HeroPassportDataExportResult(
    string SchemaVersion,
    string DestinationPath,
    long SizeBytes);

public static class HeroPassportDataExport
{
    public const string SchemaVersion = "hero-passport-export/1";

    private static readonly JsonSerializerOptions ExportJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public static async Task<HeroPassportDataExportResult> CreateAsync(
        string sourceDatabasePath,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDatabasePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        var sourcePath = Path.GetFullPath(sourceDatabasePath);
        var exportPath = Path.GetFullPath(destinationPath);
        if (string.Equals(sourcePath, exportPath, PathComparison))
        {
            throw new IOException("Export destination must differ from the active Hero Passport database.");
        }

        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("Hero Passport database was not found.", sourcePath);
        }

        if (File.Exists(exportPath))
        {
            throw new IOException("Export destination already exists and will not be overwritten.");
        }

        var sourceReport = await HeroPassportDatabaseDoctor
            .InspectAsync(sourcePath, cancellationToken)
            .ConfigureAwait(false);
        if (!sourceReport.Healthy)
        {
            throw new InvalidOperationException(
                "Export refused because the active Hero Passport database did not pass doctor validation.");
        }

        var exportDirectory = Path.GetDirectoryName(exportPath)
            ?? throw new InvalidOperationException("Export destination directory could not be resolved.");
        Directory.CreateDirectory(exportDirectory);
        var temporaryPath = Path.Combine(
            exportDirectory,
            $".{Path.GetFileName(exportPath)}.{Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)}.tmp");

        try
        {
            var document = await ReadDocumentAsync(sourcePath, cancellationToken).ConfigureAwait(false);
            await WriteCandidateAsync(temporaryPath, document, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, exportPath, overwrite: false);

            return new HeroPassportDataExportResult(
                SchemaVersion,
                exportPath,
                new FileInfo(exportPath).Length);
        }
        catch
        {
            TryDeleteUnpublishedCandidate(temporaryPath);
            throw;
        }
    }

    private static async Task<ExportDocument> ReadDocumentAsync(
        string sourcePath,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(CreateReadOnlyConnectionString(sourcePath));
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction(deferred: true);

        var settings = await ReadSettingsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        var skillsByHero = await ReadHeroSkillsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        var traitsByHero = await ReadHeroTraitsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        var titlesByHero = await ReadHeroTitlesAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        var heroes = await ReadHeroesAsync(
            connection,
            transaction,
            skillsByHero,
            traitsByHero,
            titlesByHero,
            cancellationToken).ConfigureAwait(false);

        var reportSkills = await ReadReportSkillsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        var rewardComponents = await ReadRewardComponentsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        var trustStrainComponents = await ReadTrustStrainComponentsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        var milestones = await ReadMilestonesAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        var quests = await ReadQuestsAsync(
            connection,
            transaction,
            reportSkills,
            rewardComponents,
            trustStrainComponents,
            milestones,
            cancellationToken).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new ExportDocument(SchemaVersion, settings, heroes, quests);
    }

    private static async Task<ExportSettings> ReadSettingsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            transaction,
            """
            SELECT active_hero_id, locale, presentation_style, auto_start_quest, auto_finish_quest
            FROM app_settings
            WHERE id=1;
            """);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidDataException("Hero Passport settings row is missing.");
        }

        return new ExportSettings(
            ReadNullableString(reader, 0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetInt64(3) == 1,
            reader.GetInt64(4) == 1);
    }

    private static async Task<ExportHero[]> ReadHeroesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        IReadOnlyDictionary<string, ExportHeroSkill[]> skillsByHero,
        IReadOnlyDictionary<string, ExportHeroTrait[]> traitsByHero,
        IReadOnlyDictionary<string, ExportHeroTitle[]> titlesByHero,
        CancellationToken cancellationToken)
    {
        var heroes = new List<ExportHero>();
        await using var command = CreateCommand(
            connection,
            transaction,
            """
            SELECT id, name, total_xp, trust, strain, success_streak, archived_at_utc, created_at_utc, updated_at_utc
            FROM heroes
            ORDER BY created_at_utc, id;
            """);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var heroId = reader.GetString(0);
            heroes.Add(new ExportHero(
                heroId,
                reader.GetString(1),
                reader.GetInt64(2),
                reader.GetInt32(3),
                reader.GetInt32(4),
                reader.GetInt64(5),
                ReadNullableString(reader, 6),
                reader.GetString(7),
                reader.GetString(8),
                skillsByHero.GetValueOrDefault(heroId, []),
                traitsByHero.GetValueOrDefault(heroId, []),
                titlesByHero.GetValueOrDefault(heroId, [])));
        }

        return [.. heroes];
    }

    private static async Task<Dictionary<string, ExportHeroSkill[]>> ReadHeroSkillsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        var rows = new Dictionary<string, List<ExportHeroSkill>>(StringComparer.Ordinal);
        await using var command = CreateCommand(
            connection,
            transaction,
            "SELECT hero_id, skill_key, xp FROM hero_skills ORDER BY hero_id, skill_key;");
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            AddGrouped(rows, reader.GetString(0), new ExportHeroSkill(reader.GetString(1), reader.GetInt64(2)));
        }

        return Freeze(rows);
    }

    private static async Task<Dictionary<string, ExportHeroTrait[]>> ReadHeroTraitsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        var rows = new Dictionary<string, List<ExportHeroTrait>>(StringComparer.Ordinal);
        await using var command = CreateCommand(
            connection,
            transaction,
            "SELECT hero_id, trait_key, unlocked_at_utc FROM hero_traits ORDER BY hero_id, trait_key;");
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            AddGrouped(rows, reader.GetString(0), new ExportHeroTrait(reader.GetString(1), reader.GetString(2)));
        }

        return Freeze(rows);
    }

    private static async Task<Dictionary<string, ExportHeroTitle[]>> ReadHeroTitlesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        var rows = new Dictionary<string, List<ExportHeroTitle>>(StringComparer.Ordinal);
        await using var command = CreateCommand(
            connection,
            transaction,
            "SELECT hero_id, title_key, unlocked_at_utc FROM hero_titles ORDER BY hero_id, title_key;");
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            AddGrouped(rows, reader.GetString(0), new ExportHeroTitle(reader.GetString(1), reader.GetString(2)));
        }

        return Freeze(rows);
    }

    private static async Task<Dictionary<string, ExportReportSkill[]>> ReadReportSkillsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        var rows = new Dictionary<string, List<ExportReportSkill>>(StringComparer.Ordinal);
        await using var command = CreateCommand(
            connection,
            transaction,
            """
            SELECT quest_report_id, skill_key, xp_gained, xp_before, xp_after, level_before, level_after
            FROM quest_report_skills
            ORDER BY quest_report_id, ordinal;
            """);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            AddGrouped(rows, reader.GetString(0), new ExportReportSkill(
                reader.GetString(1),
                reader.GetInt64(2),
                reader.GetInt64(3),
                reader.GetInt64(4),
                reader.GetInt32(5),
                reader.GetInt32(6)));
        }

        return Freeze(rows);
    }

    private static async Task<Dictionary<string, ExportRewardComponent[]>> ReadRewardComponentsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        var rows = new Dictionary<string, List<ExportRewardComponent>>(StringComparer.Ordinal);
        await using var command = CreateCommand(
            connection,
            transaction,
            """
            SELECT quest_report_id, component_key, xp_delta
            FROM quest_reward_components
            ORDER BY quest_report_id, ordinal;
            """);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            AddGrouped(rows, reader.GetString(0), new ExportRewardComponent(reader.GetString(1), reader.GetInt64(2)));
        }

        return Freeze(rows);
    }

    private static async Task<Dictionary<string, ExportTrustStrainComponent[]>> ReadTrustStrainComponentsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        var rows = new Dictionary<string, List<ExportTrustStrainComponent>>(StringComparer.Ordinal);
        await using var command = CreateCommand(
            connection,
            transaction,
            """
            SELECT quest_report_id, component_key, trust_delta, strain_delta
            FROM quest_trust_strain_components
            ORDER BY quest_report_id, ordinal;
            """);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            AddGrouped(rows, reader.GetString(0), new ExportTrustStrainComponent(
                reader.GetString(1),
                reader.GetInt32(2),
                reader.GetInt32(3)));
        }

        return Freeze(rows);
    }

    private static async Task<Dictionary<string, ExportMilestone[]>> ReadMilestonesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        var rows = new Dictionary<string, List<ExportMilestone>>(StringComparer.Ordinal);
        await using var command = CreateCommand(
            connection,
            transaction,
            """
            SELECT quest_report_id, event_key, semantic_key
            FROM quest_milestones
            ORDER BY quest_report_id, ordinal;
            """);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            AddGrouped(rows, reader.GetString(0), new ExportMilestone(reader.GetString(1), reader.GetString(2)));
        }

        return Freeze(rows);
    }

    private static async Task<ExportQuest[]> ReadQuestsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        IReadOnlyDictionary<string, ExportReportSkill[]> reportSkills,
        IReadOnlyDictionary<string, ExportRewardComponent[]> rewardComponents,
        IReadOnlyDictionary<string, ExportTrustStrainComponent[]> trustStrainComponents,
        IReadOnlyDictionary<string, ExportMilestone[]> milestones,
        CancellationToken cancellationToken)
    {
        var quests = new List<ExportQuest>();
        await using var command = CreateCommand(
            connection,
            transaction,
            """
            SELECT
                q.id, q.hero_id, p.display_name, q.quest_type, q.title, q.goal, q.locale, q.status,
                q.started_at_utc, q.finished_at_utc,
                r.id, r.result, r.summary, r.tests_mentioned, r.scope_violations, r.user_corrections,
                r.build_status, r.build_evidence, r.tests_status, r.tests_evidence,
                r.reward_rule_version, r.hero_progression_version, r.skill_progression_version,
                r.skill_allocation_version, r.trust_strain_rule_version, r.streak_rule_version,
                r.unlock_rule_version, r.rank_rule_version,
                r.base_xp, r.bonus_xp, r.penalty_xp, r.raw_xp, r.outcome_permille, r.xp_gained,
                r.hero_total_xp_before, r.hero_total_xp_after, r.hero_level_before, r.hero_level_after,
                r.rank_before, r.rank_after, r.trust_before, r.trust_after, r.strain_before, r.strain_after,
                r.streak_before, r.streak_after, r.active_title_before, r.active_title_after, r.created_at_utc
            FROM quest_sessions q
            JOIN projects p ON p.id=q.project_id
            LEFT JOIN quest_reports r ON r.quest_id=q.id
            ORDER BY q.started_at_utc, q.id;
            """);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var reportId = ReadNullableString(reader, 10);
            ExportQuestReport? report = null;
            if (reportId is not null)
            {
                report = new ExportQuestReport(
                    reader.GetString(11),
                    reader.GetString(12),
                    reader.GetInt64(13) == 1,
                    reader.GetInt32(14),
                    reader.GetInt32(15),
                    reader.GetString(16),
                    reader.GetString(17),
                    reader.GetString(18),
                    reader.GetString(19),
                    new ExportRuleVersions(
                        reader.GetString(20), reader.GetString(21), reader.GetString(22), reader.GetString(23),
                        reader.GetString(24), reader.GetString(25), reader.GetString(26), reader.GetString(27)),
                    reader.GetInt32(28),
                    reader.GetInt32(29),
                    reader.GetInt32(30),
                    reader.GetInt32(31),
                    reader.GetInt32(32),
                    reader.GetInt64(33),
                    reader.GetInt64(34),
                    reader.GetInt64(35),
                    reader.GetInt32(36),
                    reader.GetInt32(37),
                    reader.GetString(38),
                    reader.GetString(39),
                    reader.GetInt32(40),
                    reader.GetInt32(41),
                    reader.GetInt32(42),
                    reader.GetInt32(43),
                    reader.GetInt64(44),
                    reader.GetInt64(45),
                    ReadNullableString(reader, 46),
                    ReadNullableString(reader, 47),
                    reader.GetString(48),
                    reportSkills.GetValueOrDefault(reportId, []),
                    rewardComponents.GetValueOrDefault(reportId, []),
                    trustStrainComponents.GetValueOrDefault(reportId, []),
                    milestones.GetValueOrDefault(reportId, []));
            }

            quests.Add(new ExportQuest(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.GetString(8),
                ReadNullableString(reader, 9),
                report));
        }

        return [.. quests];
    }

    private static async Task WriteCandidateAsync(
        string temporaryPath,
        ExportDocument document,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            temporaryPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous);
        await JsonSerializer.SerializeAsync(stream, document, ExportJsonOptions, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static SqliteCommand CreateCommand(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string commandText)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        return command;
    }

    private static string CreateReadOnlyConnectionString(string sourcePath)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = sourcePath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Default,
            ForeignKeys = true,
            Pooling = false,
            DefaultTimeout = 5,
        };
        return builder.ToString();
    }

    private static string? ReadNullableString(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static void AddGrouped<T>(Dictionary<string, List<T>> groups, string key, T value)
    {
        if (!groups.TryGetValue(key, out var group))
        {
            group = [];
            groups.Add(key, group);
        }

        group.Add(value);
    }

    private static Dictionary<string, T[]> Freeze<T>(Dictionary<string, List<T>> groups) =>
        groups.ToDictionary(static pair => pair.Key, static pair => pair.Value.ToArray(), StringComparer.Ordinal);

    private static void TryDeleteUnpublishedCandidate(string temporaryPath)
    {
        try
        {
            File.Delete(temporaryPath);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private sealed record ExportDocument(
        string SchemaVersion,
        ExportSettings Settings,
        ExportHero[] Heroes,
        ExportQuest[] Quests);

    private sealed record ExportSettings(
        string? ActiveHeroId,
        string Locale,
        string PresentationStyle,
        bool AutoStartQuest,
        bool AutoFinishQuest);

    private sealed record ExportHero(
        string HeroId,
        string Name,
        long TotalXp,
        int Trust,
        int Strain,
        long SuccessStreak,
        string? ArchivedAtUtc,
        string CreatedAtUtc,
        string UpdatedAtUtc,
        ExportHeroSkill[] Skills,
        ExportHeroTrait[] Traits,
        ExportHeroTitle[] Titles);

    private sealed record ExportHeroSkill(string SkillKey, long Xp);
    private sealed record ExportHeroTrait(string TraitKey, string UnlockedAtUtc);
    private sealed record ExportHeroTitle(string TitleKey, string UnlockedAtUtc);

    private sealed record ExportQuest(
        string QuestId,
        string HeroId,
        string ProjectDisplayName,
        string QuestType,
        string Title,
        string Goal,
        string Locale,
        string Status,
        string StartedAtUtc,
        string? FinishedAtUtc,
        ExportQuestReport? Report);

    private sealed record ExportQuestReport(
        string Result,
        string Summary,
        bool TestsMentioned,
        int ScopeViolations,
        int UserCorrections,
        string BuildStatus,
        string BuildEvidence,
        string TestsStatus,
        string TestsEvidence,
        ExportRuleVersions RuleVersions,
        int BaseXp,
        int BonusXp,
        int PenaltyXp,
        int RawXp,
        int OutcomePermille,
        long XpGained,
        long HeroTotalXpBefore,
        long HeroTotalXpAfter,
        int HeroLevelBefore,
        int HeroLevelAfter,
        string RankBefore,
        string RankAfter,
        int TrustBefore,
        int TrustAfter,
        int StrainBefore,
        int StrainAfter,
        long StreakBefore,
        long StreakAfter,
        string? ActiveTitleBefore,
        string? ActiveTitleAfter,
        string CreatedAtUtc,
        ExportReportSkill[] Skills,
        ExportRewardComponent[] RewardComponents,
        ExportTrustStrainComponent[] TrustStrainComponents,
        ExportMilestone[] Milestones);

    private sealed record ExportRuleVersions(
        string Reward,
        string HeroProgression,
        string SkillProgression,
        string SkillAllocation,
        string TrustStrain,
        string Streak,
        string Unlock,
        string Rank);

    private sealed record ExportReportSkill(
        string SkillKey,
        long XpGained,
        long XpBefore,
        long XpAfter,
        int LevelBefore,
        int LevelAfter);

    private sealed record ExportRewardComponent(string ComponentKey, long XpDelta);
    private sealed record ExportTrustStrainComponent(string ComponentKey, int TrustDelta, int StrainDelta);
    private sealed record ExportMilestone(string EventKey, string SemanticKey);
}
