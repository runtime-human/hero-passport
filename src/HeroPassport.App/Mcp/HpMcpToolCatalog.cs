using ModelContextProtocol.Protocol;
using System.Text.Json;

namespace HeroPassport.App.Mcp;

public static class HpMcpToolCatalog
{
    private const string Uuid7Pattern = "^[0-9a-f]{8}-[0-9a-f]{4}-7[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$";
    private const string UuidPlaceholder = "$UUID7$";

    public static IReadOnlyList<Tool> ProtocolTools { get; } =
    [
        Create("hero.bootstrap", readOnly: false, BootstrapSchema(), BootstrapOutputSchema()),
        Create("hero.configure", readOnly: false, ConfigureSchema(), ConfigureOutputSchema()),
        Create("hero.get_context", readOnly: true, EmptySchema(), ContextOutputSchema()),
        Create("hero.create", readOnly: false, CreateHeroSchema(), CreateHeroOutputSchema()),
        Create("hero.list", readOnly: true, EmptySchema(), ListHeroesOutputSchema()),
        Create("hero.activate", readOnly: false, HeroIdSchema(), PreferenceOutputSchema()),
        Create("hero.archive", readOnly: false, HeroIdSchema(), PreferenceOutputSchema()),
        Create("hero.restore", readOnly: false, HeroIdSchema(), PreferenceOutputSchema()),
        Create("hero.start_quest", readOnly: false, StartQuestSchema(), StartQuestOutputSchema()),
        Create("hero.finish_quest", readOnly: false, FinishQuestSchema(), FinishQuestOutputSchema()),
        Create("hero.get_card", readOnly: true, HeroIdSchema(), CardOutputSchema()),
    ];

    private static Tool Create(string name, bool readOnly, JsonElement inputSchema, JsonElement outputSchema) => new()
    {
        Name = name,
        Description = name switch
        {
            "hero.bootstrap" => "Initialize Hero Passport once with retry-safe first-run settings and the initial Hero.",
            "hero.configure" => "Update typed Hero Passport presentation and automation preferences after setup.",
            "hero.get_context" => "Hydrate setup, active Hero, current Project, open Quests, and compatibility versions without mutating state.",
            "hero.create" => "Create an additional Hero with a caller-provided retry identity; creation does not activate it.",
            "hero.list" => "List Heroes with active, archive, progression, Trust, and Strain projections.",
            "hero.activate" => "Set the default Hero for future Quest formation without moving existing Quests.",
            "hero.archive" => "Reversibly archive an inactive Hero that owns no open Quest.",
            "hero.restore" => "Restore an archived Hero without activating it.",
            "hero.start_quest" => "Start one retry-safe Quest for an explicit Hero in the invocation-bound Project.",
            "hero.finish_quest" => "Atomically finalize a Quest with bounded attestations and conflict-safe retry semantics.",
            "hero.get_card" => "Read one explicit Hero card and its projection for the invocation-bound Project.",
            _ => name,
        },
        InputSchema = inputSchema,
        OutputSchema = outputSchema,
        Annotations = new ToolAnnotations
        {
            ReadOnlyHint = readOnly,
            DestructiveHint = false,
            IdempotentHint = true,
            OpenWorldHint = false,
        },
    };

    private static JsonElement EmptySchema() => Parse("""
        {"type":"object","properties":{},"required":[],"additionalProperties":false}
        """);

    private static JsonElement HeroIdSchema() => ParseWithUuid("""
        {"type":"object","properties":{"heroId":{"type":"string","pattern":"$UUID7$"}},"required":["heroId"],"additionalProperties":false}
        """);

    private static JsonElement BootstrapSchema() => ParseWithUuid("""
        {
          "type":"object",
          "properties":{
            "bootstrapRequestId":{"type":"string","pattern":"$UUID7$"},
            "locale":{"type":"string","enum":["ru-RU","en-US"]},
            "heroName":{"type":"string","minLength":1,"maxLength":64},
            "presentationStyle":{"type":"string","enum":["rpg_engineering","classic_rpg","minimal"]},
            "autoStartQuest":{"type":"boolean"},
            "autoFinishQuest":{"type":"boolean"}
          },
          "required":["bootstrapRequestId","locale","heroName","presentationStyle","autoStartQuest","autoFinishQuest"],
          "additionalProperties":false
        }
        """);

    private static JsonElement ConfigureSchema() => Parse("""
        {
          "type":"object",
          "properties":{
            "locale":{"type":"string","enum":["ru-RU","en-US"]},
            "presentationStyle":{"type":"string","enum":["rpg_engineering","classic_rpg","minimal"]},
            "autoStartQuest":{"type":"boolean"},
            "autoFinishQuest":{"type":"boolean"}
          },
          "required":["locale","presentationStyle","autoStartQuest","autoFinishQuest"],
          "additionalProperties":false
        }
        """);

    private static JsonElement CreateHeroSchema() => ParseWithUuid("""
        {
          "type":"object",
          "properties":{
            "createRequestId":{"type":"string","pattern":"$UUID7$"},
            "name":{"type":"string","minLength":1,"maxLength":64}
          },
          "required":["createRequestId","name"],
          "additionalProperties":false
        }
        """);

    private static JsonElement StartQuestSchema() => ParseWithUuid("""
        {
          "type":"object",
          "properties":{
            "startRequestId":{"type":"string","pattern":"$UUID7$"},
            "heroId":{"type":"string","pattern":"$UUID7$"},
            "questType":{"type":"string","enum":["planning","research","coding","review","debugging","documentation","maintenance"]},
            "title":{"type":"string","minLength":1,"maxLength":120},
            "goal":{"type":"string","minLength":1,"maxLength":500}
          },
          "required":["startRequestId","heroId","questType","title","goal"],
          "additionalProperties":false
        }
        """);

    private static JsonElement FinishQuestSchema() => ParseWithUuid("""
        {
          "type":"object",
          "properties":{
            "finishRequestId":{"type":"string","pattern":"$UUID7$"},
            "questId":{"type":"string","pattern":"$UUID7$"},
            "result":{"type":"string","enum":["success","partial","blocked","failed","abandoned"]},
            "summary":{"type":"string","minLength":1,"maxLength":2000},
            "metrics":{
              "type":"object",
              "properties":{
                "testsMentioned":{"type":"boolean"},
                "scopeViolations":{"type":"integer","minimum":0,"maximum":20},
                "userCorrections":{"type":"integer","minimum":0,"maximum":20},
                "buildStatus":{"type":"string","enum":["not_run","passed","failed","unknown"]},
                "buildEvidence":{"type":"string","enum":["observed","reported","none"]},
                "testsStatus":{"type":"string","enum":["not_run","passed","failed","unknown"]},
                "testsEvidence":{"type":"string","enum":["observed","reported","none"]}
              },
              "required":["testsMentioned","scopeViolations","userCorrections","buildStatus","buildEvidence","testsStatus","testsEvidence"],
              "additionalProperties":false
            },
            "skillsUsed":{
              "type":"array",
              "items":{"type":"string","enum":["coding","testing_awareness","scope_control","documentation","tool_use","planning","research","debugging","review","maintenance"]},
              "minItems":1,
              "maxItems":3,
              "uniqueItems":true
            }
          },
          "required":["finishRequestId","questId","result","summary","metrics","skillsUsed"],
          "additionalProperties":false
        }
        """);

    private static JsonElement BootstrapOutputSchema() => ParseWithUuid("""
        {
          "type":"object",
          "properties":{
            "setupCompleted":{"type":"boolean"},
            "hero":{"type":"object","properties":{"heroId":{"type":"string","pattern":"$UUID7$"},"name":{"type":"string"}},"required":["heroId","name"],"additionalProperties":false},
            "settings":{"type":"object","properties":{"locale":{"type":"string"},"presentationStyle":{"type":"string"},"autoStartQuest":{"type":"boolean"},"autoFinishQuest":{"type":"boolean"}},"required":["locale","presentationStyle","autoStartQuest","autoFinishQuest"],"additionalProperties":false},
            "replayed":{"type":"boolean"},
            "displayText":{"type":"string"}
          },
          "required":["setupCompleted","hero","settings","replayed","displayText"],
          "additionalProperties":false
        }
        """);

    private static JsonElement ConfigureOutputSchema() => Parse("""
        {
          "type":"object",
          "properties":{
            "settings":{"type":"object","properties":{"locale":{"type":"string"},"presentationStyle":{"type":"string"},"autoStartQuest":{"type":"boolean"},"autoFinishQuest":{"type":"boolean"}},"required":["locale","presentationStyle","autoStartQuest","autoFinishQuest"],"additionalProperties":false},
            "changed":{"type":"boolean"},
            "displayText":{"type":"string"}
          },
          "required":["settings","changed","displayText"],
          "additionalProperties":false
        }
        """);

    private static JsonElement ContextOutputSchema() => ParseWithUuid("""
        {
          "type":"object",
          "properties":{
            "productVersion":{"type":"string"},
            "contractVersion":{"type":"string"},
            "skillContractVersion":{"type":"string"},
            "setupCompleted":{"type":"boolean"},
            "settings":{"type":"object","properties":{"locale":{"type":"string"},"presentationStyle":{"type":"string"},"autoStartQuest":{"type":"boolean"},"autoFinishQuest":{"type":"boolean"}},"required":["locale","presentationStyle","autoStartQuest","autoFinishQuest"],"additionalProperties":false},
            "activeHero":{"type":"object","properties":{"heroId":{"type":"string","pattern":"$UUID7$"},"name":{"type":"string"}},"required":["heroId","name"],"additionalProperties":false},
            "project":{"type":"object","properties":{"displayName":{"type":"string"}},"required":["displayName"],"additionalProperties":false},
            "openQuests":{"type":"array","items":{"type":"object","properties":{"questId":{"type":"string","pattern":"$UUID7$"},"heroId":{"type":"string","pattern":"$UUID7$"},"heroName":{"type":"string"},"questType":{"type":"string"},"title":{"type":"string"},"goal":{"type":"string"},"startedAtUtc":{"type":"string"},"locale":{"type":"string"}},"required":["questId","heroId","heroName","questType","title","goal","startedAtUtc","locale"],"additionalProperties":false}},
            "ruleVersions":{"type":"object","properties":{"reward":{"type":"string"},"heroProgression":{"type":"string"},"skillProgression":{"type":"string"},"skillAllocation":{"type":"string"},"trustStrain":{"type":"string"},"streak":{"type":"string"},"unlock":{"type":"string"},"rank":{"type":"string"}},"required":["reward","heroProgression","skillProgression","skillAllocation","trustStrain","streak","unlock","rank"],"additionalProperties":false},
            "displayText":{"type":"string"}
          },
          "required":["productVersion","contractVersion","skillContractVersion","setupCompleted","project","openQuests","ruleVersions","displayText"],
          "additionalProperties":false
        }
        """);

    private static JsonElement CreateHeroOutputSchema() => ParseWithUuid("""
        {
          "type":"object",
          "properties":{
            "hero":{"type":"object","properties":{"heroId":{"type":"string","pattern":"$UUID7$"},"name":{"type":"string"},"level":{"type":"integer"},"rankKey":{"type":"string"},"trust":{"type":"integer"},"strain":{"type":"integer"},"archived":{"type":"boolean"}},"required":["heroId","name","level","rankKey","trust","strain","archived"],"additionalProperties":false},
            "replayed":{"type":"boolean"},
            "displayText":{"type":"string"}
          },
          "required":["hero","replayed","displayText"],
          "additionalProperties":false
        }
        """);

    private static JsonElement ListHeroesOutputSchema() => ParseWithUuid("""
        {
          "type":"object",
          "properties":{
            "heroes":{"type":"array","items":{"type":"object","properties":{"heroId":{"type":"string","pattern":"$UUID7$"},"name":{"type":"string"},"archived":{"type":"boolean"},"active":{"type":"boolean"},"totalXp":{"type":"integer","minimum":0},"level":{"type":"integer","minimum":1},"rankKey":{"type":"string"},"trust":{"type":"integer","minimum":0,"maximum":100},"strain":{"type":"integer","minimum":0,"maximum":100}},"required":["heroId","name","archived","active","totalXp","level","rankKey","trust","strain"],"additionalProperties":false}},
            "displayText":{"type":"string"}
          },
          "required":["heroes","displayText"],
          "additionalProperties":false
        }
        """);

    private static JsonElement PreferenceOutputSchema() => ParseWithUuid("""
        {
          "type":"object",
          "properties":{
            "hero":{"type":"object","properties":{"heroId":{"type":"string","pattern":"$UUID7$"},"name":{"type":"string"},"archived":{"type":"boolean"},"active":{"type":"boolean"},"totalXp":{"type":"integer","minimum":0},"level":{"type":"integer","minimum":1},"rankKey":{"type":"string"},"trust":{"type":"integer","minimum":0,"maximum":100},"strain":{"type":"integer","minimum":0,"maximum":100}},"required":["heroId","name","archived","active","totalXp","level","rankKey","trust","strain"],"additionalProperties":false},
            "changed":{"type":"boolean"},
            "displayText":{"type":"string"}
          },
          "required":["hero","changed","displayText"],
          "additionalProperties":false
        }
        """);

    private static JsonElement StartQuestOutputSchema() => ParseWithUuid("""
        {
          "type":"object",
          "properties":{
            "quest":{"type":"object","properties":{"questId":{"type":"string","pattern":"$UUID7$"},"heroId":{"type":"string","pattern":"$UUID7$"},"questType":{"type":"string"},"title":{"type":"string"},"goal":{"type":"string"},"startedAtUtc":{"type":"string"},"locale":{"type":"string"}},"required":["questId","heroId","questType","title","goal","startedAtUtc","locale"],"additionalProperties":false},
            "hero":{"type":"object","properties":{"heroId":{"type":"string","pattern":"$UUID7$"},"name":{"type":"string"},"level":{"type":"integer","minimum":1},"rankKey":{"type":"string"}},"required":["heroId","name","level","rankKey"],"additionalProperties":false},
            "replayed":{"type":"boolean"},
            "displayText":{"type":"string"}
          },
          "required":["quest","hero","replayed","displayText"],
          "additionalProperties":false
        }
        """);

    private static JsonElement FinishQuestOutputSchema() => ParseWithUuid("""
        {
          "type":"object",
          "properties":{
            "questId":{"type":"string","pattern":"$UUID7$"},
            "result":{"type":"string","enum":["success","partial","blocked","failed","abandoned"]},
            "replayed":{"type":"boolean"},
            "alreadyFinalized":{"type":"boolean"},
            "reward":{"type":"object","properties":{"baseXp":{"type":"integer","minimum":0},"bonusXp":{"type":"integer","minimum":0},"penaltyXp":{"type":"integer","minimum":0},"rawXp":{"type":"integer","minimum":0},"outcomePermille":{"type":"integer","minimum":0,"maximum":1000},"xpGained":{"type":"integer","minimum":0},"rewardRuleVersion":{"type":"string"},"components":{"type":"array","items":{"type":"object","properties":{},"required":[],"additionalProperties":false}}},"required":["baseXp","bonusXp","penaltyXp","rawXp","outcomePermille","xpGained","rewardRuleVersion","components"],"additionalProperties":false},
            "heroProgress":{"type":"object","properties":{"heroId":{"type":"string","pattern":"$UUID7$"},"totalXpBefore":{"type":"integer","minimum":0},"totalXpAfter":{"type":"integer","minimum":0},"levelBefore":{"type":"integer","minimum":1},"levelAfter":{"type":"integer","minimum":1},"isLevelCapped":{"type":"boolean"},"levelXp":{"type":"integer","minimum":0},"nextLevelXpRequired":{"type":"integer","minimum":1},"rankBefore":{"type":"string"},"rankAfter":{"type":"string"}},"required":["heroId","totalXpBefore","totalXpAfter","levelBefore","levelAfter","isLevelCapped","levelXp","rankBefore","rankAfter"],"additionalProperties":false},
            "trustStrain":{"type":"object","properties":{"trustBefore":{"type":"integer","minimum":0,"maximum":100},"trustAfter":{"type":"integer","minimum":0,"maximum":100},"strainBefore":{"type":"integer","minimum":0,"maximum":100},"strainAfter":{"type":"integer","minimum":0,"maximum":100},"components":{"type":"array","items":{"type":"object","properties":{"key":{"type":"string"},"trustDelta":{"type":"integer"},"strainDelta":{"type":"integer"}},"required":["key","trustDelta","strainDelta"],"additionalProperties":false}},"ruleVersion":{"type":"string"}},"required":["trustBefore","trustAfter","strainBefore","strainAfter","components","ruleVersion"],"additionalProperties":false},
            "streak":{"type":"object","properties":{"before":{"type":"integer","minimum":0},"after":{"type":"integer","minimum":0},"ruleVersion":{"type":"string"}},"required":["before","after","ruleVersion"],"additionalProperties":false},
            "skillProgress":{"type":"array","items":{"type":"object","properties":{"skillKey":{"type":"string"},"xpGained":{"type":"integer","minimum":0},"xpAfter":{"type":"integer","minimum":0},"levelBefore":{"type":"integer","minimum":1},"levelAfter":{"type":"integer","minimum":1},"isLevelCapped":{"type":"boolean"},"nextLevelXpRequired":{"type":"integer","minimum":1}},"required":["skillKey","xpGained","xpAfter","levelBefore","levelAfter","isLevelCapped"],"additionalProperties":false}},
            "traitsUnlocked":{"type":"array","items":{"type":"string"}},
            "titlesUnlocked":{"type":"array","items":{"type":"string"}},
            "activeTitle":{"type":"string"},
            "milestones":{"type":"array","items":{"type":"object","properties":{"eventKey":{"type":"string"},"semanticKey":{"type":"string"}},"required":["eventKey","semanticKey"],"additionalProperties":false}},
            "displayText":{"type":"string"}
          },
          "required":["questId","result","replayed","alreadyFinalized","reward","heroProgress","trustStrain","streak","skillProgress","traitsUnlocked","titlesUnlocked","milestones","displayText"],
          "additionalProperties":false
        }
        """);

    private static JsonElement CardOutputSchema() => ParseWithUuid("""
        {
          "type":"object",
          "properties":{
            "hero":{"type":"object","properties":{"heroId":{"type":"string","pattern":"$UUID7$"},"name":{"type":"string"},"totalXp":{"type":"integer","minimum":0},"level":{"type":"integer","minimum":1},"isLevelCapped":{"type":"boolean"},"levelXp":{"type":"integer","minimum":0},"nextLevelXpRequired":{"type":"integer","minimum":1},"rankKey":{"type":"string"},"activeTitle":{"type":"string"},"trust":{"type":"integer","minimum":0,"maximum":100},"strain":{"type":"integer","minimum":0,"maximum":100},"successStreak":{"type":"integer","minimum":0},"topSkills":{"type":"array","items":{"type":"object","properties":{"skillKey":{"type":"string"},"xp":{"type":"integer","minimum":0},"level":{"type":"integer","minimum":1},"isLevelCapped":{"type":"boolean"},"nextLevelXpRequired":{"type":"integer","minimum":1}},"required":["skillKey","xp","level","isLevelCapped"],"additionalProperties":false}},"traits":{"type":"array","items":{"type":"string"}},"titles":{"type":"array","items":{"type":"string"}}},"required":["heroId","name","totalXp","level","isLevelCapped","levelXp","rankKey","trust","strain","successStreak","topSkills","traits","titles"],"additionalProperties":false},
            "project":{"type":"object","properties":{"displayName":{"type":"string"},"questsStarted":{"type":"integer","minimum":0},"questsFinished":{"type":"integer","minimum":0},"questsSucceeded":{"type":"integer","minimum":0},"totalXpEarned":{"type":"integer","minimum":0},"successRatePermille":{"type":"integer","minimum":0,"maximum":1000},"topSkills":{"type":"array","items":{"type":"object","properties":{"skillKey":{"type":"string"},"xp":{"type":"integer","minimum":0},"level":{"type":"integer","minimum":1},"isLevelCapped":{"type":"boolean"},"nextLevelXpRequired":{"type":"integer","minimum":1}},"required":["skillKey","xp","level","isLevelCapped"],"additionalProperties":false}}},"required":["displayName","questsStarted","questsFinished","questsSucceeded","totalXpEarned","successRatePermille","topSkills"],"additionalProperties":false},
            "displayText":{"type":"string"}
          },
          "required":["hero","project","displayText"],
          "additionalProperties":false
        }
        """);

    private static JsonElement ParseWithUuid(string json) =>
        Parse(json.Replace(UuidPlaceholder, Uuid7Pattern, StringComparison.Ordinal));

    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
