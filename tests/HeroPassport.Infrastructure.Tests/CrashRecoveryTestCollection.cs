using Xunit;

namespace HeroPassport.Infrastructure.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CrashRecoveryTestCollection
{
    public const string Name = "Crash recovery qualification";
}
