using HeroPassport.Application.Runtime;
using Xunit;

namespace HeroPassport.Application.Tests;

public sealed class CanonicalMutationEncoderTests
{
    [Fact]
    public void BootstrapHashMatchesMutationArgsV1GoldenVector()
    {
        var hash = CanonicalMutationEncoder.HashBootstrap(
            "ru-RU",
            "Нова",
            "rpg_engineering",
            autoStartQuest: true,
            autoFinishQuest: false);

        Assert.Equal(
            Convert.FromHexString("13CB24CF88C7012A072808EE6C6C10BAECA348ABD0199027703B54D01666431D"),
            hash);
    }

    [Fact]
    public void CreateHeroHashMatchesMutationArgsV1GoldenVector()
    {
        var hash = CanonicalMutationEncoder.HashCreateHero("Герой");

        Assert.Equal(
            Convert.FromHexString("33AAA489D7A5E22AB6E3310C8A9D4337AECAD7CAFF25E63B26C18E292E863A13"),
            hash);
    }
}
