using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Engine;
using HeroPassport.Domain.Primitives;
using Microsoft.Data.Sqlite;
using System.Globalization;

namespace HeroPassport.Infrastructure.Persistence;

public sealed partial class SqliteHeroPassportStateStore
{
    private static async Task<PreparedUnlockProgress> PrepareUnlockProgressAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        HeroId heroId,
        string questType,
        FinishQuestStoreCommand command,
        int heroLevelBefore,
        int heroLevelAfter,
        string rankBefore,
        string rankAfter,
        long streakBefore,
        long streakAfter,
        PreparedSkillProgressSet preparedSkills,
        string unlockRuleVersion,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var counters = await LifetimeUnlockCountersAsync(
            connection,
            transaction,
            heroId,
            cancellationToken).ConfigureAwait(false);
        counters = counters with
        {
            PreciseExecutorSuccesses = checked(counters.PreciseExecutorSuccesses +
                (IsPreciseExecutorSuccess(command) ? 1L : 0L)),
            TestScoutSuccesses = checked(counters.TestScoutSuccesses +
                (IsTestScoutSuccess(questType, command) ? 1L : 0L)),
            ScopeKeeperSuccesses = checked(counters.ScopeKeeperSuccesses +
                (IsScopeKeeperSuccess(command) ? 1L : 0L)),
        };

        var existingTraits = await ExistingTraitKeysAsync(
            connection,
            transaction,
            heroId,
            cancellationToken).ConfigureAwait(false);
        var existingTitleStates = await ExistingTitleStatesAsync(
            connection,
            transaction,
            heroId,
            cancellationToken).ConfigureAwait(false);
        var existingTitleKeys = existingTitleStates
            .Select(static title => title.TitleKey)
            .ToArray();
        var activeTitleBefore = UnlockRules.SelectActiveTitle(existingTitleStates, unlockRuleVersion);

        var unlock = UnlockRules.Evaluate(
            new UnlockEvaluationContext(
                heroLevelBefore,
                heroLevelAfter,
                rankBefore,
                rankAfter,
                streakBefore,
                streakAfter,
                counters.PreciseExecutorSuccesses,
                counters.TestScoutSuccesses,
                counters.ScopeKeeperSuccesses,
                preparedSkills.SkillsAfter,
                preparedSkills.LevelChanges,
                existingTraits,
                existingTitleKeys),
            unlockRuleVersion);

        var allTitleStates = new List<TitleUnlockState>(existingTitleStates.Count + unlock.TitlesUnlocked.Count);
        allTitleStates.AddRange(existingTitleStates);
        foreach (var titleKey in unlock.TitlesUnlocked)
        {
            allTitleStates.Add(new TitleUnlockState(titleKey, now));
        }

        return new PreparedUnlockProgress(
            unlock,
            activeTitleBefore,
            UnlockRules.SelectActiveTitle(allTitleStates, unlockRuleVersion));
    }

    private static async Task PersistUnlockProgressAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        QuestReportId reportId,
        QuestId questId,
        HeroId heroId,
        PreparedUnlockProgress prepared,
        string timestamp,
        CancellationToken cancellationToken)
    {
        foreach (var traitKey in prepared.Result.TraitsUnlocked)
        {
            await ExecuteAsync(
                connection,
                transaction,
                "INSERT INTO hero_traits(hero_id,trait_key,unlocked_at_utc,source_quest_id) VALUES($hero,$key,$time,$quest);",
                cancellationToken,
                ("$hero", heroId.ToString()),
                ("$key", traitKey),
                ("$time", timestamp),
                ("$quest", questId.ToString())).ConfigureAwait(false);
        }

        foreach (var titleKey in prepared.Result.TitlesUnlocked)
        {
            await ExecuteAsync(
                connection,
                transaction,
                "INSERT INTO hero_titles(hero_id,title_key,unlocked_at_utc,source_quest_id) VALUES($hero,$key,$time,$quest);",
                cancellationToken,
                ("$hero", heroId.ToString()),
                ("$key", titleKey),
                ("$time", timestamp),
                ("$quest", questId.ToString())).ConfigureAwait(false);
        }

        for (var ordinal = 0; ordinal < prepared.Result.Milestones.Count; ordinal++)
        {
            var milestone = prepared.Result.Milestones[ordinal];
            await ExecuteAsync(
                connection,
                transaction,
                "INSERT INTO quest_milestones(quest_report_id,ordinal,event_key,semantic_key) VALUES($report,$ordinal,$event,$semantic);",
                cancellationToken,
                ("$report", reportId.ToString()),
                ("$ordinal", ordinal),
                ("$event", milestone.EventKey),
                ("$semantic", milestone.SemanticKey)).ConfigureAwait(false);
        }
    }

    private static async Task<StoredUnlockResult> StoredUnlockResultAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        QuestReportId reportId,
        CancellationToken cancellationToken)
    {
        await using var command = Command(
            connection,
            transaction,
            "SELECT event_key,semantic_key FROM quest_milestones WHERE quest_report_id=$report ORDER BY ordinal;",
            ("$report", reportId.ToString()));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var milestones = new List<MilestoneSnapshot>();
        var traits = new List<string>();
        var titles = new List<string>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var eventKey = reader.GetString(0);
            var semanticKey = reader.GetString(1);
            milestones.Add(new MilestoneSnapshot(eventKey, semanticKey));
            if (string.Equals(eventKey, "trait_unlocked", StringComparison.Ordinal) &&
                semanticKey.StartsWith("trait:", StringComparison.Ordinal))
            {
                traits.Add(semanticKey["trait:".Length..]);
            }
            else if (string.Equals(eventKey, "title_unlocked", StringComparison.Ordinal) &&
                     semanticKey.StartsWith("title:", StringComparison.Ordinal))
            {
                titles.Add(semanticKey["title:".Length..]);
            }
        }

        return new StoredUnlockResult(traits.ToArray(), titles.ToArray(), milestones.ToArray());
    }

    private static async Task<HeroUnlockCard> HeroUnlockCardAsync(
        SqliteConnection connection,
        HeroId heroId,
        string unlockRuleVersion,
        CancellationToken cancellationToken)
    {
        var traits = await ExistingTraitKeysAsync(
            connection,
            transaction: null,
            heroId,
            cancellationToken).ConfigureAwait(false);
        var titleStates = await ExistingTitleStatesAsync(
            connection,
            transaction: null,
            heroId,
            cancellationToken).ConfigureAwait(false);
        var titles = titleStates
            .Select(static title => title.TitleKey)
            .OrderBy(static key => key, StringComparer.Ordinal)
            .ToArray();
        return new HeroUnlockCard(
            traits,
            titles,
            UnlockRules.SelectActiveTitle(titleStates, unlockRuleVersion));
    }

    private static async Task<LifetimeUnlockCounters> LifetimeUnlockCountersAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        HeroId heroId,
        CancellationToken cancellationToken)
    {
        await using var command = Command(
            connection,
            transaction,
            """
            SELECT
                COALESCE(SUM(CASE
                    WHEN r.result='success' AND r.scope_violations=0 AND r.user_corrections=0 THEN 1 ELSE 0 END),0),
                COALESCE(SUM(CASE
                    WHEN r.result='success' AND q.quest_type IN ('coding','debugging')
                         AND r.tests_status='passed' AND r.tests_evidence='observed' THEN 1 ELSE 0 END),0),
                COALESCE(SUM(CASE
                    WHEN r.result='success' AND r.scope_violations=0 THEN 1 ELSE 0 END),0)
            FROM quest_reports AS r
            INNER JOIN quest_sessions AS q ON q.id=r.quest_id
            WHERE q.hero_id=$hero;
            """,
            ("$hero", heroId.ToString()));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return new LifetimeUnlockCounters(0, 0, 0);
        }

        return new LifetimeUnlockCounters(reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2));
    }

    private static async Task<IReadOnlyList<string>> ExistingTraitKeysAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        HeroId heroId,
        CancellationToken cancellationToken)
    {
        await using var command = Command(
            connection,
            transaction,
            "SELECT trait_key FROM hero_traits WHERE hero_id=$hero ORDER BY trait_key;",
            ("$hero", heroId.ToString()));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var traits = new List<string>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            traits.Add(reader.GetString(0));
        }
        return traits.ToArray();
    }

    private static async Task<IReadOnlyList<TitleUnlockState>> ExistingTitleStatesAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        HeroId heroId,
        CancellationToken cancellationToken)
    {
        await using var command = Command(
            connection,
            transaction,
            "SELECT title_key,unlocked_at_utc FROM hero_titles WHERE hero_id=$hero ORDER BY title_key;",
            ("$hero", heroId.ToString()));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var titles = new List<TitleUnlockState>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            titles.Add(new TitleUnlockState(reader.GetString(0), ParseTimestamp(reader.GetString(1))));
        }
        return titles.ToArray();
    }

    private static DateTimeOffset ParseTimestamp(string value) =>
        DateTimeOffset.ParseExact(
            value,
            "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

    private static bool IsPreciseExecutorSuccess(FinishQuestStoreCommand command) =>
        string.Equals(command.Result, "success", StringComparison.Ordinal) &&
        command.Metrics.ScopeViolations == 0 &&
        command.Metrics.UserCorrections == 0;

    private static bool IsTestScoutSuccess(string questType, FinishQuestStoreCommand command) =>
        string.Equals(command.Result, "success", StringComparison.Ordinal) &&
        questType is "coding" or "debugging" &&
        string.Equals(command.Metrics.TestsStatus, "passed", StringComparison.Ordinal) &&
        string.Equals(command.Metrics.TestsEvidence, "observed", StringComparison.Ordinal);

    private static bool IsScopeKeeperSuccess(FinishQuestStoreCommand command) =>
        string.Equals(command.Result, "success", StringComparison.Ordinal) &&
        command.Metrics.ScopeViolations == 0;

    private static MilestoneSnapshot[] MilestoneSnapshots(UnlockResult unlock) =>
        unlock.Milestones
            .Select(static milestone => new MilestoneSnapshot(milestone.EventKey, milestone.SemanticKey))
            .ToArray();

    private sealed record LifetimeUnlockCounters(
        long PreciseExecutorSuccesses,
        long TestScoutSuccesses,
        long ScopeKeeperSuccesses);

    private sealed record PreparedUnlockProgress(
        UnlockResult Result,
        string? ActiveTitleBefore,
        string? ActiveTitleAfter);

    private sealed record StoredUnlockResult(
        IReadOnlyList<string> TraitsUnlocked,
        IReadOnlyList<string> TitlesUnlocked,
        IReadOnlyList<MilestoneSnapshot> Milestones);

    private sealed record HeroUnlockCard(
        IReadOnlyList<string> Traits,
        IReadOnlyList<string> Titles,
        string? ActiveTitle);
}
