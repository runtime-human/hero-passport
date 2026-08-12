using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using ModelContextProtocol;
using System.ComponentModel;
using System.Globalization;

namespace HeroPassport.App.Mcp;

public sealed class HeroPassportMcpEndpoint
{
    private readonly HeroPassportApplication? _application;
    private readonly ProjectBindingContext _project;

    public HeroPassportMcpEndpoint(HeroPassportApplication application, ProjectBindingContext project)
    {
        _application = application ?? throw new ArgumentNullException(nameof(application));
        _project = project ?? throw new ArgumentNullException(nameof(project));
    }

    private HeroPassportMcpEndpoint()
    {
        _project = new ProjectBindingContext("metadata", new string('0', 64), "project-identity/1");
    }

    public static HeroPassportMcpEndpoint CreateForMetadataTests() => new();

    [Description("Create the initial Hero Passport hero and preferences exactly once with crash-safe replay.")]
    public Task<McpBootstrapResult> BootstrapAsync(
        string bootstrapRequestId,
        string locale,
        string heroName,
        string presentationStyle,
        bool autoStartQuest,
        bool autoFinishQuest,
        CancellationToken cancellationToken) => ExecuteAsync(async () =>
        {
            var result = await Application.BootstrapAsync(
                new BootstrapRequest(ParseRequestId(bootstrapRequestId), locale, heroName, presentationStyle, autoStartQuest, autoFinishQuest),
                cancellationToken).ConfigureAwait(false);
            return new McpBootstrapResult(
                result.SetupCompleted,
                Hero(result.Hero),
                Settings(result.Settings),
                result.Replayed,
                result.Replayed ? "Hero Passport setup already committed; replayed the original result." : $"Hero {result.Hero.Name} created.");
        });

    [Description("Update Hero Passport locale, presentation, and automatic Quest preferences after setup.")]
    public Task<McpConfigureResult> ConfigureAsync(
        string locale,
        string presentationStyle,
        bool autoStartQuest,
        bool autoFinishQuest,
        CancellationToken cancellationToken) => ExecuteAsync(async () =>
        {
            var result = await Application.ConfigureAsync(
                new ConfigureRequest(locale, presentationStyle, autoStartQuest, autoFinishQuest),
                cancellationToken).ConfigureAwait(false);
            return new McpConfigureResult(Settings(result.Settings), result.Changed, result.Changed ? "Hero Passport preferences updated." : "Hero Passport preferences already match.");
        });

    [Description("Read current Hero Passport settings, active Hero, project context, open Quests, and compatibility versions without mutating state.")]
    public Task<McpRuntimeContextResult> GetContextAsync(CancellationToken cancellationToken) => ExecuteAsync(async () =>
    {
        var result = await Application.GetRuntimeContextAsync(_project, cancellationToken).ConfigureAwait(false);
        return new McpRuntimeContextResult(
            result.ProductVersion,
            result.ContractVersion,
            result.SkillContractVersion,
            result.SetupCompleted,
            result.Settings is null ? null : Settings(result.Settings),
            result.ActiveHero is null ? null : Hero(result.ActiveHero),
            new McpProjectContext(result.Project.DisplayName),
            result.OpenQuests.Select(static quest => new McpOpenQuest(
                quest.QuestId.ToString(), quest.HeroId.ToString(), quest.HeroName, quest.QuestType, quest.Title, quest.Goal, FormatTime(quest.StartedAtUtc), quest.Locale)).ToArray(),
            result.RuleVersions,
            result.SetupCompleted ? "Hero Passport context loaded." : "Hero Passport setup is required.");
    });

    [Description("Create an additional Hero without changing the active Hero.")]
    public Task<McpCreateHeroResult> CreateHeroAsync(string createRequestId, string name, CancellationToken cancellationToken) => ExecuteAsync(async () =>
    {
        var result = await Application.CreateHeroAsync(new CreateHeroRequest(ParseRequestId(createRequestId), name), cancellationToken).ConfigureAwait(false);
        return new McpCreateHeroResult(Hero(result.Hero), result.Replayed, result.Replayed ? "Hero creation replayed." : $"Hero {result.Hero.Name} created.");
    });

    [Description("List all Hero Passport heroes in stable order.")]
    public Task<McpListHeroesResult> ListHeroesAsync(CancellationToken cancellationToken) => ExecuteAsync(async () =>
    {
        var result = await Application.ListHeroesAsync(cancellationToken).ConfigureAwait(false);
        return new McpListHeroesResult(
            result.Heroes.Select(static hero => new McpHeroListItem(
                hero.HeroId.ToString(), hero.Name, hero.Archived, hero.Active, hero.TotalXp, hero.Level, hero.RankKey, hero.Trust, hero.Strain)).ToArray(),
            $"{result.Heroes.Count.ToString(CultureInfo.InvariantCulture)} hero(s).");
    });

    [Description("Make an existing non-archived Hero the default for future Quest formation.")]
    public Task<McpActivationResult> ActivateHeroAsync(string heroId, CancellationToken cancellationToken) => ExecuteAsync(async () =>
    {
        var parsed = ParseHeroId(heroId);
        await Application.ActivateHeroAsync(parsed, cancellationToken).ConfigureAwait(false);
        return new McpActivationResult(parsed.ToString(), true, "Active Hero updated.");
    });

    [Description("Archive a non-active Hero that has no open Quest.")]
    public Task<McpLifecycleResult> ArchiveHeroAsync(string heroId, CancellationToken cancellationToken) => ExecuteAsync(async () =>
    {
        var result = await Application.ArchiveHeroAsync(ParseHeroId(heroId), cancellationToken).ConfigureAwait(false);
        return new McpLifecycleResult(Hero(result.Hero), result.AlreadyInRequestedState, result.AlreadyInRequestedState ? "Hero was already archived." : "Hero archived.");
    });

    [Description("Restore an archived Hero without activating it.")]
    public Task<McpLifecycleResult> RestoreHeroAsync(string heroId, CancellationToken cancellationToken) => ExecuteAsync(async () =>
    {
        var result = await Application.RestoreHeroAsync(ParseHeroId(heroId), cancellationToken).ConfigureAwait(false);
        return new McpLifecycleResult(Hero(result.Hero), result.AlreadyInRequestedState, result.AlreadyInRequestedState ? "Hero was already restored." : "Hero restored.");
    });

    [Description("Start one durable Quest for an explicit Hero in the current project with caller-generated retry identity.")]
    public Task<McpStartQuestResult> StartQuestAsync(
        string startRequestId,
        string heroId,
        string questType,
        string title,
        string goal,
        CancellationToken cancellationToken) => ExecuteAsync(async () =>
        {
            var result = await Application.StartQuestAsync(
                new StartQuestRequest(ParseRequestId(startRequestId), ParseHeroId(heroId), questType, title, goal),
                _project,
                cancellationToken).ConfigureAwait(false);
            return new McpStartQuestResult(
                Quest(result.Quest),
                Hero(result.Hero),
                result.Replayed,
                result.Replayed ? $"↻ {result.Quest.Title}" : $"⚔ {result.Quest.Title}");
        });

    [Description("Finalize a durable Quest once using bounded agent attestations; conflicting finalizations are rejected.")]
    public Task<McpFinishQuestResult> FinishQuestAsync(
        string finishRequestId,
        string questId,
        string result,
        string summary,
        McpFinishMetricsInput metrics,
        string[] skillsUsed,
        CancellationToken cancellationToken) => ExecuteAsync(async () =>
        {
            ArgumentNullException.ThrowIfNull(metrics);
            ArgumentNullException.ThrowIfNull(skillsUsed);
            var finished = await Application.FinishQuestAsync(
                new FinishQuestRequest(
                    ParseRequestId(finishRequestId),
                    ParseQuestId(questId),
                    result,
                    summary,
                    new FinishMetrics(metrics.TestsMentioned, metrics.ScopeViolations, metrics.UserCorrections, metrics.BuildStatus, metrics.BuildEvidence, metrics.TestsStatus, metrics.TestsEvidence),
                    skillsUsed),
                _project,
                cancellationToken).ConfigureAwait(false);
            return new McpFinishQuestResult(
                finished.QuestId.ToString(),
                finished.Result,
                finished.Replayed,
                finished.AlreadyFinalized,
                new McpReward(
                    finished.Reward.BaseXp,
                    finished.Reward.BonusXp,
                    finished.Reward.PenaltyXp,
                    finished.Reward.RawXp,
                    finished.Reward.OutcomePermille,
                    finished.Reward.XpGained,
                    finished.Reward.RewardRuleVersion,
                    finished.Reward.Components.Select(static component => new McpRewardComponent(component.Key, component.XpDelta)).ToArray()),
                new McpHeroProgress(
                    finished.HeroProgress.HeroId.ToString(),
                    finished.HeroProgress.TotalXpBefore,
                    finished.HeroProgress.TotalXpAfter,
                    finished.HeroProgress.LevelBefore,
                    finished.HeroProgress.LevelAfter,
                    finished.HeroProgress.IsLevelCapped,
                    finished.HeroProgress.LevelXp,
                    finished.HeroProgress.NextLevelXpRequired,
                    finished.HeroProgress.RankBefore,
                    finished.HeroProgress.RankAfter),
                new McpTrustStrain(
                    finished.TrustStrain.TrustBefore,
                    finished.TrustStrain.TrustAfter,
                    finished.TrustStrain.StrainBefore,
                    finished.TrustStrain.StrainAfter,
                    finished.TrustStrain.Components.Select(static component => new McpTrustStrainComponent(component.Key, component.TrustDelta, component.StrainDelta)).ToArray(),
                    finished.TrustStrain.RuleVersion),
                new McpStreak(finished.Streak.Before, finished.Streak.After, finished.Streak.RuleVersion),
                finished.SkillProgress.Select(static skill => new McpSkillProgress(
                    skill.SkillKey,
                    skill.XpGained,
                    skill.XpAfter,
                    skill.LevelBefore,
                    skill.LevelAfter,
                    skill.IsLevelCapped,
                    skill.NextLevelXpRequired)).ToArray(),
                finished.TraitsUnlocked,
                finished.TitlesUnlocked,
                finished.ActiveTitle,
                finished.Milestones.Select(static milestone => new McpMilestone(milestone.EventKey, milestone.SemanticKey)).ToArray(),
                $"✓ Quest completed · +{finished.Reward.XpGained.ToString(CultureInfo.InvariantCulture)} XP");
        });

    [Description("Read a Hero card and current-project statistics without mutating state.")]
    public Task<McpHeroCardResult> GetCardAsync(string heroId, CancellationToken cancellationToken) => ExecuteAsync(async () =>
    {
        var result = await Application.GetHeroCardAsync(ParseHeroId(heroId), _project, cancellationToken).ConfigureAwait(false);
        return new McpHeroCardResult(
            new McpHeroCardHero(
                result.Hero.HeroId.ToString(), result.Hero.Name, result.Hero.TotalXp, result.Hero.Level, result.Hero.IsLevelCapped,
                result.Hero.LevelXp, result.Hero.NextLevelXpRequired, result.Hero.RankKey, result.Hero.ActiveTitle,
                result.Hero.Trust, result.Hero.Strain, result.Hero.SuccessStreak,
                result.Hero.TopSkills.Select(Skill).ToArray(), result.Hero.Traits, result.Hero.Titles),
            new McpHeroCardProject(
                result.Project.DisplayName, result.Project.QuestsStarted, result.Project.QuestsFinished, result.Project.QuestsSucceeded,
                result.Project.TotalXpEarned, result.Project.SuccessRatePermille, result.Project.TopSkills.Select(Skill).ToArray()),
            $"{result.Hero.Name} · Level {result.Hero.Level.ToString(CultureInfo.InvariantCulture)} · {result.Hero.TotalXp.ToString(CultureInfo.InvariantCulture)} XP");
    });

    private HeroPassportApplication Application => _application ?? throw new InvalidOperationException("Metadata-only MCP endpoint cannot be invoked.");

    private static async Task<T> ExecuteAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return await action().ConfigureAwait(false);
        }
        catch (HeroPassportException exception)
        {
            throw new McpException($"{exception.Code} {exception.Message}", exception);
        }
        catch (FormatException exception)
        {
            throw new McpException("HP100 invalid_request", exception);
        }
        catch (ArgumentException exception)
        {
            throw new McpException("HP100 invalid_request", exception);
        }
    }

    private static MutationRequestId ParseRequestId(string value) => MutationRequestId.Parse(value);
    private static HeroId ParseHeroId(string value) => HeroId.Parse(value);
    private static QuestId ParseQuestId(string value) => QuestId.Parse(value);

    private static McpSettings Settings(SettingsSnapshot settings) =>
        new(settings.Locale, settings.PresentationStyle, settings.AutoStartQuest, settings.AutoFinishQuest);

    private static McpHero Hero(HeroSummary hero) =>
        new(hero.HeroId.ToString(), hero.Name, hero.TotalXp, hero.Level, hero.RankKey, hero.Trust, hero.Strain, hero.Archived);

    private static McpQuest Quest(QuestSummary quest) =>
        new(quest.QuestId.ToString(), quest.HeroId.ToString(), quest.QuestType, quest.Title, quest.Goal, FormatTime(quest.StartedAtUtc), quest.Locale);

    private static McpHeroCardSkill Skill(HeroCardSkill skill) =>
        new(skill.SkillKey, skill.Xp, skill.Level, skill.IsLevelCapped, skill.NextLevelXpRequired);

    private static string FormatTime(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
}
