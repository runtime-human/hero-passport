using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace HeroPassport.Infrastructure.Tests;

// Infrastructure qualification manipulates Microsoft.Data.Sqlite's process-wide
// connection pools and deliberately kills external processes at WAL commit
// boundaries. Running independent test classes concurrently makes those global
// provider/file-lifetime operations interfere with each other on Windows.
internal static class InfrastructureTestAssemblyConfiguration
{
}
