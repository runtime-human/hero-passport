using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Engine;
using HeroPassport.Domain.Primitives;
using Microsoft.Data.Sqlite;

namespace HeroPassport.Infrastructure.Persistence;

public sealed partial class SqliteHeroPassportStateStore
{
    public async Task<HeroSkillProgressionReadResult> GetSkillProgressionAsync(
        HeroId heroId,
        ProjectBindingContext project,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await HeroPassportDatabase
            .OpenConnectionAsync(_databasePath, cancellationToken)
            .ConfigureAwait(false);
        using var transaction = connection.BeginTransaction(deferred: true);

        var hero = await HeroAsync(
                connection,
                transaction,
                heroId.ToString(),
                cancellationToken)
            .ConfigureAwait(false);
        var existingProject = await FindSkillProgressionProjectAsync(
                connection,
                transaction,
                project.WorkspaceFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
        var heroXp = await HeroSkillXpAsync(
                connection,
                transaction,
                heroId,
                cancellationToken)
            .ConfigureAwait(false);
        var projectXp = existingProject is null
            ? EmptySkillXp()
            : await ProjectSkillContributionXpAsync(
                    connection,
                    transaction,
                    heroId,
                    existingProject.Id,
                    cancellationToken)
                .ConfigureAwait(false);

        var ruleVersion = HeroPassportVersions.CurrentRules.SkillProgression;
        var rows = new SkillProgressionReadRow[SkillCatalog.Keys.Count];
        for (var index = 0; index < SkillCatalog.Keys.Count; index++)
        {
            var skillKey = SkillCatalog.Keys[index];
            var globalXp = heroXp.GetValueOrDefault(skillKey);
            var level = SkillProgressionRules.Level(globalXp, ruleVersion);
            rows[index] = new SkillProgressionReadRow(
                skillKey,
                new SkillProgressionReadSnapshot(
                    globalXp,
                    level,
                    SkillProgressionRules.IsLevelCapped(level, ruleVersion),
                    SkillProgressionRules.LevelXp(globalXp, level, ruleVersion),
                    SkillProgressionRules.NextLevelXpRequired(level, ruleVersion)),
                new SkillProjectContributionReadSnapshot(
                    projectXp.GetValueOrDefault(skillKey)));
        }

        transaction.Commit();
        return new HeroSkillProgressionReadResult(
            hero.Name,
            existingProject?.DisplayName ?? project.DisplayName,
            rows);
    }

    private static async Task<SkillProgressionProjectRow?> FindSkillProgressionProjectAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string workspaceFingerprint,
        CancellationToken cancellationToken)
    {
        await using var command = Command(
            connection,
            transaction,
            "SELECT id,display_name FROM projects WHERE workspace_fingerprint=$fingerprint LIMIT 1;",
            ("$fingerprint", workspaceFingerprint));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new SkillProgressionProjectRow(reader.GetString(0), reader.GetString(1));
    }

    private static async Task<Dictionary<string, long>> HeroSkillXpAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        HeroId heroId,
        CancellationToken cancellationToken)
    {
        await using var command = Command(
            connection,
            transaction,
            "SELECT skill_key,xp FROM hero_skills WHERE hero_id=$hero;",
            ("$hero", heroId.ToString()));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var xpBySkill = EmptySkillXp();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var skillKey = reader.GetString(0);
            if (!SkillCatalog.Contains(skillKey))
            {
                throw new InvalidOperationException(
                    "Persisted Hero Skill projection contains a non-canonical Skill key.");
            }

            xpBySkill.Add(skillKey, JsonSafeInteger.Require(reader.GetInt64(1)));
        }

        return xpBySkill;
    }

    private static async Task<Dictionary<string, long>> ProjectSkillContributionXpAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        HeroId heroId,
        string projectId,
        CancellationToken cancellationToken)
    {
        await using var command = Command(
            connection,
            transaction,
            """
            SELECT report_skill.skill_key,SUM(report_skill.xp_gained)
            FROM quest_sessions AS quest
            INNER JOIN quest_reports AS report ON report.quest_id=quest.id
            INNER JOIN quest_report_skills AS report_skill ON report_skill.quest_report_id=report.id
            WHERE quest.hero_id=$hero
              AND quest.project_id=$project
              AND quest.status='finished'
            GROUP BY report_skill.skill_key;
            """,
            ("$hero", heroId.ToString()),
            ("$project", projectId));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var xpBySkill = EmptySkillXp();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var skillKey = reader.GetString(0);
            if (!SkillCatalog.Contains(skillKey))
            {
                throw new InvalidOperationException(
                    "Canonical Quest Skill history contains a non-canonical Skill key.");
            }

            xpBySkill.Add(skillKey, JsonSafeInteger.Require(reader.GetInt64(1)));
        }

        return xpBySkill;
    }

    private static Dictionary<string, long> EmptySkillXp() =>
        new(StringComparer.Ordinal);

    private sealed record SkillProgressionProjectRow(string Id, string DisplayName);
}
