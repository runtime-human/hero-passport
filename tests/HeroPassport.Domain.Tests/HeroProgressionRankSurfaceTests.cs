using HeroPassport.Domain.Engine;
using Xunit;

namespace HeroPassport.Domain.Tests;

public sealed class HeroProgressionRankSurfaceTests
{
    [Fact]
    public void VersionedHeroProgressionAndRankAuthoritiesExist()
    {
        var assembly = typeof(SkillProgressionRules).Assembly;

        var heroProgression = assembly.GetType("HeroPassport.Domain.Engine.HeroProgressionRules");
        var rank = assembly.GetType("HeroPassport.Domain.Engine.RankRules");

        Assert.NotNull(heroProgression);
        Assert.NotNull(rank);
        Assert.Equal(
            "hero-progression/2.0.0",
            heroProgression.GetField("RuleVersion")?.GetRawConstantValue());
        Assert.Equal(
            "rank/1.0.0",
            rank.GetField("RuleVersion")?.GetRawConstantValue());
    }

    [Fact]
    public void TransitionalMinimalFinishFacadeIsRemoved()
    {
        var assembly = typeof(HeroProgressionRules).Assembly;

        Assert.Null(assembly.GetType("HeroPassport.Domain.Engine.MinimalQuestFinishRules"));
    }
}
