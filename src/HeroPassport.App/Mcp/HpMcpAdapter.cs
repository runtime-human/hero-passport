using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using HeroPassport.Infrastructure.ProjectIdentity;
using ModelContextProtocol.Protocol;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HeroPassport.App.Mcp;

public sealed class HpMcpAdapter(
    HeroPassportApplication application,
    Func<CancellationToken, Task<ProjectBindingContext>> projectProvider)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<CallToolResult> InvokeAsync(
        string toolName,
        IDictionary<string, JsonElement>? arguments,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return toolName switch
            {
                "hero.bootstrap" => await BootstrapAsync(arguments, cancellationToken).ConfigureAwait(false),
                "hero.configure" => await ConfigureAsync(arguments, cancellationToken).ConfigureAwait(false),
                "hero.get_context" => await GetContextAsync(arguments, cancellationToken).ConfigureAwait(false),
                "hero.create" => await CreateHeroAsync(arguments, cancellationToken).ConfigureAwait(false),
                "hero.list" => await ListHeroesAsync(arguments, cancellationToken).ConfigureAwait(false),
                "hero.activate" => await ActivateHeroAsync(arguments, cancellationToken).ConfigureAwait(false),
                "hero.archive" => await ArchiveHeroAsync(arguments, cancellationToken).ConfigureAwait(false),
                "hero.restore" => await RestoreHeroAsync(arguments, cancellationToken).ConfigureAwait(false),
                "hero.start_quest" => await StartQuestAsync(arguments, cancellationToken).ConfigureAwait(false),
                "hero.finish_quest" => await FinishQuestAsync(arguments, cancellationToken).ConfigureAwait(false),
                "hero.get_card" => await GetCardAsync(arguments, cancellationToken).ConfigureAwait(false),
                _ => HpMcpResponses.Error("HP100", "Unknown Hero Passport tool."),
            };
        }
        catch (HeroPassportException exception)
        {
            return HpMcpResponses.Error(exception.Code, exception.Message);
        }
        catch (ProjectIdentityException exception)
        {
            return HpMcpResponses.Error(exception.Code, exception.Message);
        }
        catch (ArgumentException)
        {
            return HpMcpResponses.Error("HP100", "Invalid request.");
        }
        catch (FormatException)
        {
            return HpMcpResponses.Error("HP100", "Invalid request.");
        }
    }

    private async Task<CallToolResult> BootstrapAsync(IDictionary<string, JsonElement>? arguments, CancellationToken token)
    {
        var args = RequireExact(arguments, "bootstrapRequestId", "locale", "heroName", "presentationStyle", "autoStartQuest", "autoFinishQuest");
        var result = await application.BootstrapAsync(
            new BootstrapRequest(
                MutationRequestId.Parse(RequireString(args, "bootstrapRequestId")),
                RequireString(args, "locale"),
                RequireString(args, "heroName"),
                RequireString(args, "presentationStyle"),
                RequireBool(args, "autoStartQuest"),
                RequireBool(args, "autoFinishQuest")),
            token).ConfigureAwait(false);

        return Success(new
        {
            setupCompleted = true,
            hero = Identity(result.Hero),
            settings = Settings(result.Settings),
            result.Replayed,
            displayText = $"Hero Passport is ready for {result.Hero.Name}.",
        });
    }

    private async Task<CallToolResult> ConfigureAsync(IDictionary<string, JsonElement>? arguments, CancellationToken token)
    {
        var args = RequireExact(arguments, "locale", "presentationStyle", "autoStartQuest", "autoFinishQuest");
        var result = await application.ConfigureAsync(
            new ConfigureRequest(
                RequireString(args, "locale"),
                RequireString(args, "presentationStyle"),
                RequireBool(args, "autoStartQuest"),
                RequireBool(args, "autoFinishQuest")),
            token).ConfigureAwait(false);
        return Success(new
        {
            settings = Settings(result.Settings),
            result.Changed,
            displayText = result.Changed ? "Hero Passport settings updated." : "Hero Passport settings are unchanged.",
        });
    }

    private async Task<CallToolResult> GetContextAsync(IDictionary<string, JsonElement>? arguments, CancellationToken token)
    {
        _ = RequireExact(arguments);
        var project = await projectProvider(token).ConfigureAwait(false);
        var context = await application.GetRuntimeContextAsync(project, token).ConfigureAwait(false);
        return Success(new
        {
            context.ProductVersion,
            context.ContractVersion,
            context.SkillContractVersion,
            context.SetupCompleted,
            settings = context.Settings is null ? null : Settings(context.Settings),
            activeHero = context.ActiveHero is null ? null : Identity(context.ActiveHero),
            project = new { context.Project.DisplayName },
            openQuests = context.OpenQuests.Select(static quest => new
            {
                questId = quest.QuestId.ToString(),
                heroId = quest.HeroId.ToString(),
                quest.HeroName,
                quest.QuestType,
                quest.Title,
                quest.Goal,
                startedAtUtc = FormatTimestamp(quest.StartedAtUtc),
                quest.Locale,
            }).ToArray(),
            ruleVersions = new
            {
                context.RuleVersions.Reward,
                context.RuleVersions.HeroProgression,
                context.RuleVersions.SkillProgression,
                context.RuleVersions.SkillAllocation,
                context.RuleVersions.TrustStrain,
                context.RuleVersions.Streak,
                context.RuleVersions.Unlock,
                context.RuleVersions.Rank,
            },
            displayText = context.SetupCompleted ? "Hero Passport context hydrated." : "Hero Passport setup is required.",
        });
    }

    private async Task<CallToolResult> CreateHeroAsync(IDictionary<string, JsonElement>? arguments, CancellationToken token)
    {
        var args = RequireExact(arguments, "createRequestId", "name");
        var created = await application.CreateHeroAsync(
            new CreateHeroRequest(
                MutationRequestId.Parse(RequireString(args, "createRequestId")),
                RequireString(args, "name")),
            token).ConfigureAwait(false);
        var hero = (await application.ListHeroesAsync(token).ConfigureAwait(false)).Heroes.Single(item => item.HeroId == created.Hero.HeroId);
        return Success(new
        {
            hero = HeroCreateItem(hero),
            created.Replayed,
            displayText = $"Hero {hero.Name} created.",
        });
    }

    private async Task<CallToolResult> ListHeroesAsync(IDictionary<string, JsonElement>? arguments, CancellationToken token)
    {
        _ = RequireExact(arguments);
        var result = await application.ListHeroesAsync(token).ConfigureAwait(false);
        return Success(new
        {
            heroes = result.Heroes.Select(HeroListItem).ToArray(),
            displayText = $"{result.Heroes.Count} Hero(s).",
        });
    }

    private Task<CallToolResult> ActivateHeroAsync(IDictionary<string, JsonElement>? arguments, CancellationToken token) =>
        HeroPreferenceAsync(arguments, application.ActivateHeroPreferenceAsync, "activated", "already active", token);

    private Task<CallToolResult> ArchiveHeroAsync(IDictionary<string, JsonElement>? arguments, CancellationToken token) =>
        HeroPreferenceAsync(arguments, application.ArchiveHeroAsync, "archived", "already archived", token);

    private Task<CallToolResult> RestoreHeroAsync(IDictionary<string, JsonElement>? arguments, CancellationToken token) =>
        HeroPreferenceAsync(arguments, application.RestoreHeroAsync, "restored", "already restored", token);

    private static async Task<CallToolResult> HeroPreferenceAsync(
        IDictionary<string, JsonElement>? arguments,
        Func<HeroId, CancellationToken, Task<HeroPreferenceChangeResult>> action,
        string changedText,
        string unchangedText,
        CancellationToken token)
    {
        var args = RequireExact(arguments, "heroId");
        var result = await action(HeroId.Parse(RequireString(args, "heroId")), token).ConfigureAwait(false);
        return Success(new
        {
            hero = HeroListItem(result.Hero),
            result.Changed,
            displayText = $"Hero {result.Hero.Name} {(result.Changed ? changedText : unchangedText)}.",
        });
    }

    private async Task<CallToolResult> StartQuestAsync(IDictionary<string, JsonElement>? arguments, CancellationToken token)
    {
        var args = RequireExact(arguments, "startRequestId", "heroId", "questType", "title", "goal");
        var project = await projectProvider(token).ConfigureAwait(false);
        var result = await application.StartQuestAsync(
            new StartQuestRequest(
                MutationRequestId.Parse(RequireString(args, "startRequestId")),
                HeroId.Parse(RequireString(args, "heroId")),
                RequireString(args, "questType"),
                RequireString(args, "title"),
                RequireString(args, "goal")),
            project,
            token).ConfigureAwait(false);
        var hero = (await application.ListHeroesAsync(token).ConfigureAwait(false)).Heroes.Single(item => item.HeroId == result.Hero.HeroId);
        return Success(new
        {
            quest = new
            {
                questId = result.Quest.QuestId.ToString(),
                heroId = result.Quest.HeroId.ToString(),
                result.Quest.QuestType,
                result.Quest.Title,
                result.Quest.Goal,
                startedAtUtc = FormatTimestamp(result.Quest.StartedAtUtc),
                result.Quest.Locale,
            },
            hero = new
            {
                heroId = hero.HeroId.ToString(),
                hero.Name,
                hero.Level,
                hero.RankKey,
            },
            result.Replayed,
            displayText = result.Replayed ? "Quest start replayed." : "Quest started.",
        });
    }

    private async Task<CallToolResult> FinishQuestAsync(IDictionary<string, JsonElement>? arguments, CancellationToken token)
    {
        var args = RequireExact(arguments, "finishRequestId", "questId", "result", "summary", "metrics", "skillsUsed");
        var metricsElement = RequireElement(args, "metrics", JsonValueKind.Object);
        var metrics = metricsElement.EnumerateObject().ToDictionary(static property => property.Name, static property => property.Value, StringComparer.Ordinal);
        _ = RequireExact(metrics, "testsMentioned", "scopeViolations", "userCorrections", "buildStatus", "buildEvidence", "testsStatus", "testsEvidence");
        var skillsElement = RequireElement(args, "skillsUsed", JsonValueKind.Array);
        var skills = skillsElement.EnumerateArray().Select(static value =>
            value.ValueKind == JsonValueKind.String
                ? value.GetString()!
                : throw new HeroPassportException("HP100", "Invalid request.")).ToArray();
        var project = await projectProvider(token).ConfigureAwait(false);
        var result = await application.FinishQuestAsync(
            new FinishQuestRequest(
                MutationRequestId.Parse(RequireString(args, "finishRequestId")),
                QuestId.Parse(RequireString(args, "questId")),
                RequireString(args, "result"),
                RequireString(args, "summary"),
                new FinishQuestMetrics(
                    RequireBool(metrics, "testsMentioned"),
                    RequireInt32(metrics, "scopeViolations"),
                    RequireInt32(metrics, "userCorrections"),
                    RequireString(metrics, "buildStatus"),
                    RequireString(metrics, "buildEvidence"),
                    RequireString(metrics, "testsStatus"),
                    RequireString(metrics, "testsEvidence")),
                skills),
            project,
            token).ConfigureAwait(false);

        return Success(new
        {
            questId = result.QuestId.ToString(),
            result.Result,
            result.Replayed,
            result.AlreadyFinalized,
            reward = new
            {
                result.Reward.BaseXp,
                result.Reward.BonusXp,
                result.Reward.PenaltyXp,
                result.Reward.RawXp,
                result.Reward.OutcomePermille,
                result.Reward.XpGained,
                result.Reward.RewardRuleVersion,
                components = Array.Empty<object>(),
            },
            heroProgress = new
            {
                heroId = result.HeroProgress.HeroId.ToString(),
                result.HeroProgress.TotalXpBefore,
                result.HeroProgress.TotalXpAfter,
                result.HeroProgress.LevelBefore,
                result.HeroProgress.LevelAfter,
                result.HeroProgress.IsLevelCapped,
                result.HeroProgress.LevelXp,
                result.HeroProgress.NextLevelXpRequired,
                result.HeroProgress.RankBefore,
                result.HeroProgress.RankAfter,
            },
            trustStrain = result.TrustStrain,
            streak = result.Streak,
            skillProgress = result.SkillProgress,
            traitsUnlocked = result.TraitsUnlocked,
            titlesUnlocked = result.TitlesUnlocked,
            activeTitle = ExplicitNullableString(result.ActiveTitle),
            milestones = result.Milestones,
            displayText = result.Replayed ? "Quest finish replayed." : result.AlreadyFinalized ? "Quest was already finalized with the same payload." : "Quest finished.",
        });
    }

    private async Task<CallToolResult> GetCardAsync(IDictionary<string, JsonElement>? arguments, CancellationToken token)
    {
        var args = RequireExact(arguments, "heroId");
        var project = await projectProvider(token).ConfigureAwait(false);
        var result = await application.GetCardAsync(HeroId.Parse(RequireString(args, "heroId")), project, token).ConfigureAwait(false);
        return Success(new
        {
            hero = new
            {
                heroId = result.Hero.HeroId.ToString(),
                result.Hero.Name,
                result.Hero.TotalXp,
                result.Hero.Level,
                result.Hero.IsLevelCapped,
                result.Hero.LevelXp,
                result.Hero.NextLevelXpRequired,
                result.Hero.RankKey,
                activeTitle = ExplicitNullableString(result.Hero.ActiveTitle),
                result.Hero.Trust,
                result.Hero.Strain,
                result.Hero.SuccessStreak,
                topSkills = result.Hero.TopSkills,
                traits = result.Hero.Traits,
                titles = result.Hero.Titles,
            },
            project = new
            {
                result.Project.DisplayName,
                result.Project.QuestsStarted,
                result.Project.QuestsFinished,
                result.Project.QuestsSucceeded,
                result.Project.TotalXpEarned,
                result.Project.SuccessRatePermille,
                topSkills = result.Project.TopSkills,
            },
            displayText = $"Hero card for {result.Hero.Name}.",
        });
    }

    private static object Identity(HeroIdentitySnapshot hero) => new { heroId = hero.HeroId.ToString(), hero.Name };

    private static object Settings(SettingsSnapshot settings) => new
    {
        settings.Locale,
        settings.PresentationStyle,
        settings.AutoStartQuest,
        settings.AutoFinishQuest,
    };

    private static object HeroCreateItem(HeroListItemSnapshot hero) => new
    {
        heroId = hero.HeroId.ToString(),
        hero.Name,
        hero.Level,
        hero.RankKey,
        hero.Trust,
        hero.Strain,
        hero.Archived,
    };

    private static object HeroListItem(HeroListItemSnapshot hero) => new
    {
        heroId = hero.HeroId.ToString(),
        hero.Name,
        hero.Archived,
        hero.Active,
        hero.TotalXp,
        hero.Level,
        hero.RankKey,
        hero.Trust,
        hero.Strain,
    };

    private static JsonElement ExplicitNullableString(string? value) =>
        JsonSerializer.SerializeToElement<string?>(value, JsonOptions);

    private static CallToolResult Success<T>(T value) =>
        HpMcpResponses.Success(JsonSerializer.SerializeToElement(value, JsonOptions));

    private static Dictionary<string, JsonElement> RequireExact(IDictionary<string, JsonElement>? arguments, params string[] required)
    {
        var args = arguments is null
            ? new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            : new Dictionary<string, JsonElement>(arguments, StringComparer.Ordinal);
        if (args.Count != required.Length || required.Any(name => !args.ContainsKey(name)))
        {
            throw new HeroPassportException("HP100", "Invalid request.");
        }

        return args;
    }

    private static string RequireString(Dictionary<string, JsonElement> args, string name)
    {
        var value = RequireElement(args, name, JsonValueKind.String).GetString();
        return value ?? throw new HeroPassportException("HP100", "Invalid request.");
    }

    private static bool RequireBool(Dictionary<string, JsonElement> args, string name)
    {
        var element = args.TryGetValue(name, out var value) ? value : throw new HeroPassportException("HP100", "Invalid request.");
        return element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new HeroPassportException("HP100", "Invalid request."),
        };
    }

    private static int RequireInt32(Dictionary<string, JsonElement> args, string name)
    {
        var element = RequireElement(args, name, JsonValueKind.Number);
        return element.TryGetInt32(out var value) ? value : throw new HeroPassportException("HP100", "Invalid request.");
    }

    private static JsonElement RequireElement(Dictionary<string, JsonElement> args, string name, JsonValueKind kind)
    {
        if (!args.TryGetValue(name, out var value) || value.ValueKind != kind)
        {
            throw new HeroPassportException("HP100", "Invalid request.");
        }

        return value;
    }

    private static string FormatTimestamp(DateTimeOffset value) => value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture);
}
