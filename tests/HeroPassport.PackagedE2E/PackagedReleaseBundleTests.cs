using Xunit;

namespace HeroPassport.PackagedE2E;

public sealed class PackagedReleaseBundleTests
{
    [Fact]
    public void PublishedBundleContainsUserFacingDocsLicenseAndNoInternalPlans()
    {
        var publishDirectory = Environment.GetEnvironmentVariable("HERO_PASSPORT_PUBLISH_DIR");
        Assert.False(string.IsNullOrWhiteSpace(publishDirectory));
        publishDirectory = Path.GetFullPath(publishDirectory!);

        Assert.True(File.Exists(Path.Combine(publishDirectory, "HeroPassport.App.dll")));
        Assert.True(File.Exists(Path.Combine(publishDirectory, "LICENSE")), "Release bundle must ship the project license.");
        Assert.True(File.Exists(Path.Combine(publishDirectory, "README.md")), "Release bundle must ship user-facing entry documentation.");
        Assert.True(File.Exists(Path.Combine(publishDirectory, "docs", "CONFIGURATION.md")));
        Assert.True(File.Exists(Path.Combine(publishDirectory, "docs", "DISTRIBUTION.md")));
        Assert.True(File.Exists(Path.Combine(publishDirectory, "docs", "integrations", "CODEX.md")));
        Assert.False(
            Directory.Exists(Path.Combine(publishDirectory, "docs", "superpowers")),
            "Internal implementation plans are not release-bundle documentation.");
    }
}
