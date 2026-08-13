using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using Xunit;

namespace HeroPassport.Application.Tests;

public sealed class BootstrapContractTests
{
    [Fact]
    public void BootstrapRequestPreservesCallerRetryIdentity()
    {
        var requestId = MutationRequestId.New();
        var request = new BootstrapRequest(
            requestId,
            "ru-RU",
            "Nova",
            "rpg_engineering",
            true,
            true);

        Assert.Equal(requestId, request.BootstrapRequestId);
        Assert.Equal("Nova", request.HeroName);
    }
}
