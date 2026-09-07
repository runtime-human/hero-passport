using HeroPassport.App.Mcp;
using System.Text.Json;
using Xunit;

namespace HeroPassport.Contract.Tests;

public sealed class HeroCardTopSkillsContractTests
{
    [Fact]
    public void CardTopSkillsArraysAreBoundedToThreeEntries()
    {
        var output = Assert.IsType<JsonElement>(
            HpMcpToolCatalog.ProtocolTools.Single(static tool => tool.Name == "hero.get_card").OutputSchema);
        var properties = output.GetProperty("properties");

        var heroTopSkills = properties.GetProperty("hero").GetProperty("properties").GetProperty("topSkills");
        var projectTopSkills = properties.GetProperty("project").GetProperty("properties").GetProperty("topSkills");

        Assert.Equal(3, heroTopSkills.GetProperty("maxItems").GetInt32());
        Assert.Equal(3, projectTopSkills.GetProperty("maxItems").GetInt32());
    }
}
