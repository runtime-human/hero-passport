using ModelContextProtocol.Protocol;
using System.Text.Json;

namespace HeroPassport.App.Mcp;

public static class HpMcpToolCatalog
{
    private const string Uuid7Pattern = "^[0-9a-f]{8}-[0-9a-f]{4}-7[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$";
    private const string UuidPlaceholder = "$UUID7$";

    public static IReadOnlyList<Tool> ProtocolTools { get; } =
    [
        Create("hero.bootstrap", readOnly: false, BootstrapSchema()),
        Create("hero.configure", readOnly: false, ConfigureSchema()),
        Create("hero.get_context", readOnly: true, EmptySchema()),
        Create("hero.create", readOnly: false, CreateHeroSchema()),
        Create("hero.list", readOnly: true, EmptySchema()),
        Create("hero.activate", readOnly: false, HeroIdSchema()),
        Create("hero.archive", readOnly: false, HeroIdSchema()),
        Create("hero.restore", readOnly: false, HeroIdSchema()),
        Create("hero.start_quest", readOnly: false, StartQuestSchema()),
        Create("hero.finish_quest", readOnly: false, FinishQuestSchema()),
        Create("hero.get_card", readOnly: true, HeroIdSchema()),
    ];

    private static Tool Create(string name, bool readOnly, JsonElement inputSchema) => new()
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

    private static JsonElement ParseWithUuid(string json) =>
        Parse(json.Replace(UuidPlaceholder, Uuid7Pattern, StringComparison.Ordinal));

    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
