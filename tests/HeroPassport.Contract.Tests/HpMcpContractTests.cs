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
            Assert.False(annotations.DestructiveHint);
            Assert.True(annotations.IdempotentHint);
            Assert.False(annotations.OpenWorldHint);
            Assert.Equal(tool.Name is "hero.get_context" or "hero.list" or "hero.get_card", annotations.ReadOnlyHint);

            Assert.Equal("object", tool.InputSchema.GetProperty("type").GetString());
            Assert.False(tool.InputSchema.GetProperty("additionalProperties").GetBoolean());
        }
    }

    [Fact]
    public void StartAndFinishSchemasExposeExplicitRetryAndOwnershipFields()
    {
        var start = HpMcpToolCatalog.ProtocolTools.Single(static tool => tool.Name == "hero.start_quest").InputSchema;
        var startRequired = start.GetProperty("required").EnumerateArray().Select(static value => value.GetString()).ToArray();
        Assert.Contains("startRequestId", startRequired);
        Assert.Contains("heroId", startRequired);
        Assert.DoesNotContain("projectId", start.GetProperty("properties").EnumerateObject().Select(static property => property.Name));

        var finish = HpMcpToolCatalog.ProtocolTools.Single(static tool => tool.Name == "hero.finish_quest").InputSchema;
        var finishRequired = finish.GetProperty("required").EnumerateArray().Select(static value => value.GetString()).ToArray();
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
}
