using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
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

    [Fact]
    public void StartQuestHashMatchesMutationArgsV1GoldenVector()
    {
        var hash = CanonicalMutationEncoder.HashStartQuest(
            ProjectId.Parse("01900000-0000-7000-8000-000000000111"),
            HeroId.Parse("01900000-0000-7000-8000-000000000222"),
            "coding",
            "Добавить onboarding",
            "Сделать durable Start");

        Assert.Equal(
            Convert.FromHexString("8FB4E7F982F72001728F2ED6262026D371E9D0CA8816372339ED4B0C2AD38B88"),
            hash);
    }
}
