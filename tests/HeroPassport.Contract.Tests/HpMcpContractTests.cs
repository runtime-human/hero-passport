using HeroPassport.App.Mcp;
using ModelContextProtocol.Protocol;
using System.Text.Json;
using Xunit;

namespace HeroPassport.Contract.Tests;

public sealed class HpMcpContractTests
{
    private static readonly string[] ExpectedNames =
    [
        "hero.bootstrap",
        "hero.configure",
        "hero.get_context",
        "hero.create",
        "hero.list",
        "hero.activate",
        "hero.archive",
        "hero.restore",
        "hero.start_quest",
        "hero.finish_quest",
        "hero.get_card",
    ];

    [Fact]
    public void CatalogHasExactOrderAnnotationsAndClosedRootSchemas()
    {
        var tools = HpMcpToolCatalog.ProtocolTools;
        Assert.Equal(ExpectedNames, tools.Select(static tool => tool.Name).ToArray());
        Assert.DoesNotContain(tools, static tool => tool.Name is "hero.delete" or "hero.list_active_quests");

        foreach (var tool in tools)
        {
            var annotations = Assert.IsType<ToolAnnotations>(tool.Annotations);
            var expectedDestructive = tool.Name is
                "hero.configure" or "hero.activate" or "hero.archive" or "hero.restore" or "hero.finish_quest";
            Assert.Equal(expectedDestructive, annotations.DestructiveHint);
            Assert.True(annotations.IdempotentHint);
            Assert.False(annotations.OpenWorldHint);
            Assert.Equal(tool.Name is "hero.get_context" or "hero.list" or "hero.get_card", annotations.ReadOnlyHint);

            Assert.Equal("object", tool.InputSchema.GetProperty("type").GetString());
            Assert.False(tool.InputSchema.GetProperty("additionalProperties").GetBoolean());
        }
    }

    [Fact]
    public void EveryToolPublishesClosedStructuredOutputSchema()
    {
        foreach (var tool in HpMcpToolCatalog.ProtocolTools)
        {
            var output = Assert.IsType<JsonElement>(tool.OutputSchema);
            Assert.Equal("object", output.GetProperty("type").GetString());
            Assert.False(output.GetProperty("additionalProperties").GetBoolean());
            Assert.Contains("displayText", RequiredNames(output));
        }
    }

    [Fact]
    public void EveryObjectSchemaIsClosedRecursively()
    {
        foreach (var tool in HpMcpToolCatalog.ProtocolTools)
        {
            AssertObjectSchemasClosed(tool.InputSchema);
            AssertObjectSchemasClosed(Assert.IsType<JsonElement>(tool.OutputSchema));
        }
    }

    [Fact]
    public void StartAndFinishSchemasExposeExplicitRetryAndOwnershipFields()
    {
        var start = HpMcpToolCatalog.ProtocolTools.Single(static tool => tool.Name == "hero.start_quest").InputSchema;
        var startRequired = RequiredNames(start);
        Assert.Contains("startRequestId", startRequired);
        Assert.Contains("heroId", startRequired);
        Assert.DoesNotContain("projectId", start.GetProperty("properties").EnumerateObject().Select(static property => property.Name));

        var finish = HpMcpToolCatalog.ProtocolTools.Single(static tool => tool.Name == "hero.finish_quest").InputSchema;
        var finishRequired = RequiredNames(finish);
        Assert.Contains("finishRequestId", finishRequired);
        Assert.Contains("questId", finishRequired);

        var metrics = finish.GetProperty("properties").GetProperty("metrics");
        Assert.False(metrics.GetProperty("additionalProperties").GetBoolean());
        var skills = finish.GetProperty("properties").GetProperty("skillsUsed");
        Assert.Equal(1, skills.GetProperty("minItems").GetInt32());
        Assert.Equal(3, skills.GetProperty("maxItems").GetInt32());
        Assert.True(skills.GetProperty("uniqueItems").GetBoolean());
    }

    [Fact]
    public void FinishOutputSchemaPublishesFullImmutableWireShape()
    {
        var output = Assert.IsType<JsonElement>(HpMcpToolCatalog.ProtocolTools.Single(static tool => tool.Name == "hero.finish_quest").OutputSchema);
        AssertRequired(output,
            "questId", "result", "replayed", "alreadyFinalized", "reward", "heroProgress", "trustStrain", "streak",
            "skillProgress", "traitsUnlocked", "titlesUnlocked", "milestones", "displayText");

        var properties = output.GetProperty("properties");
        Assert.DoesNotContain("activeTitle", RequiredNames(output));
        Assert.Equal("string", properties.GetProperty("activeTitle").GetProperty("type").GetString());

        var reward = properties.GetProperty("reward");
        AssertClosedObject(reward);
        AssertRequired(reward,
            "baseXp", "bonusXp", "penaltyXp", "rawXp", "outcomePermille", "xpGained", "rewardRuleVersion", "components");
        var rewardComponent = reward.GetProperty("properties").GetProperty("components").GetProperty("items");
        AssertClosedObject(rewardComponent);
        AssertRequired(rewardComponent, "key", "xpDelta");

        AssertClosedObject(properties.GetProperty("heroProgress"));
        AssertClosedObject(properties.GetProperty("trustStrain"));
        AssertClosedObject(properties.GetProperty("streak"));
        AssertRequired(properties.GetProperty("heroProgress"),
            "heroId", "totalXpBefore", "totalXpAfter", "levelBefore", "levelAfter", "isLevelCapped", "levelXp", "rankBefore", "rankAfter");
        Assert.True(properties.GetProperty("heroProgress").GetProperty("properties").TryGetProperty("nextLevelXpRequired", out _));

        var skillItem = properties.GetProperty("skillProgress").GetProperty("items");
        AssertClosedObject(skillItem);
        AssertRequired(skillItem, "skillKey", "xpGained", "xpAfter", "levelBefore", "levelAfter", "isLevelCapped");
        Assert.True(skillItem.GetProperty("properties").TryGetProperty("nextLevelXpRequired", out _));

        var milestoneItem = properties.GetProperty("milestones").GetProperty("items");
        AssertClosedObject(milestoneItem);
        AssertRequired(milestoneItem, "eventKey", "semanticKey");
    }

    [Fact]
    public void CardOutputSchemaPublishesCapAwareHeroProjection()
    {
        var output = Assert.IsType<JsonElement>(HpMcpToolCatalog.ProtocolTools.Single(static tool => tool.Name == "hero.get_card").OutputSchema);
        AssertRequired(output, "hero", "project", "displayText");
        var properties = output.GetProperty("properties");
        var hero = properties.GetProperty("hero");
        AssertClosedObject(hero);
        AssertRequired(hero,
            "heroId", "name", "totalXp", "level", "isLevelCapped", "levelXp", "rankKey",
            "trust", "strain", "successStreak", "topSkills", "traits", "titles");
        Assert.DoesNotContain("activeTitle", RequiredNames(hero));
        Assert.Equal("string", hero.GetProperty("properties").GetProperty("activeTitle").GetProperty("type").GetString());
        Assert.True(hero.GetProperty("properties").TryGetProperty("nextLevelXpRequired", out _));
        AssertClosedObject(properties.GetProperty("project"));

        var skill = hero.GetProperty("properties").GetProperty("topSkills").GetProperty("items");
        AssertClosedObject(skill);
        AssertRequired(skill, "skillKey", "xp", "level", "isLevelCapped");
        Assert.True(skill.GetProperty("properties").TryGetProperty("nextLevelXpRequired", out _));
    }

    [Fact]
    public void EmptyInputToolsUseClosedEmptyObjects()
    {
        foreach (var name in new[] { "hero.get_context", "hero.list" })
        {
            var schema = HpMcpToolCatalog.ProtocolTools.Single(tool => tool.Name == name).InputSchema;
            Assert.Empty(schema.GetProperty("properties").EnumerateObject());
            Assert.Empty(schema.GetProperty("required").EnumerateArray());
        }
    }

    [Fact]
    public void SuccessDuplicatesStructuredJsonAsExactlyOneTextBlockAndErrorsDoNotExposeStructuredContent()
    {
        using var document = JsonDocument.Parse("{\"setupCompleted\":true,\"displayText\":\"Ready\"}");
        var success = HpMcpResponses.Success(document.RootElement);
        var structured = Assert.IsType<JsonElement>(success.StructuredContent);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(success.Content));
        using var compatibility = JsonDocument.Parse(text.Text);
        Assert.True(JsonElement.DeepEquals(structured, compatibility.RootElement));
        Assert.NotEqual(true, success.IsError);

        var error = HpMcpResponses.Error("HP001", "Setup is required.");
        Assert.True(error.IsError);
        Assert.Null(error.StructuredContent);
        var errorText = Assert.IsType<TextContentBlock>(Assert.Single(error.Content));
        Assert.Contains("HP001", errorText.Text, StringComparison.Ordinal);
    }

    private static string?[] RequiredNames(JsonElement schema) =>
        schema.GetProperty("required").EnumerateArray().Select(static value => value.GetString()).ToArray();

    private static void AssertRequired(JsonElement schema, params string[] names)
    {
        var required = RequiredNames(schema);
        foreach (var name in names)
        {
            Assert.Contains(name, required);
        }
    }

    private static void AssertClosedObject(JsonElement schema)
    {
        Assert.Equal("object", schema.GetProperty("type").GetString());
        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
    }

    private static void AssertObjectSchemasClosed(JsonElement schema)
    {
        if (schema.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (schema.TryGetProperty("type", out var type) &&
            type.ValueKind == JsonValueKind.String &&
            string.Equals(type.GetString(), "object", StringComparison.Ordinal))
        {
            Assert.True(schema.TryGetProperty("properties", out var objectProperties));
            Assert.Equal(JsonValueKind.Object, objectProperties.ValueKind);
            Assert.True(schema.TryGetProperty("required", out var required));
            Assert.Equal(JsonValueKind.Array, required.ValueKind);
            Assert.True(schema.TryGetProperty("additionalProperties", out var additionalProperties));
            Assert.False(additionalProperties.GetBoolean());
        }

        if (schema.TryGetProperty("properties", out var properties))
        {
            foreach (var property in properties.EnumerateObject())
            {
                AssertObjectSchemasClosed(property.Value);
            }
        }

        if (schema.TryGetProperty("items", out var items))
        {
            AssertObjectSchemasClosed(items);
        }
    }
}
