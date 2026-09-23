using Xunit;

namespace HeroPassport.Web.Tests;

public sealed class SkillProgressionStaticSsrTests
{
    [Fact]
    public void SkillsPageIsAuthenticatedGetOnlyStaticSsrSurface()
    {
        var source = ReadPage("Skills.razor");

        Assert.Contains("@page \"/skills\"", source, StringComparison.Ordinal);
        Assert.Contains("@inject HeroPassportSkillProgressionService SkillFlow", source, StringComparison.Ordinal);
        Assert.Contains("LoadAsync", source, StringComparison.Ordinal);
        Assert.Contains("Hero progression", source, StringComparison.Ordinal);
        Assert.Contains("Current project", source, StringComparison.Ordinal);
        Assert.Contains("ProjectContributionXp", source, StringComparison.Ordinal);
        Assert.Contains("href=\"/\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Project level", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NavigationManager", source, StringComparison.Ordinal);
        AssertGetOnlyStaticSsr(source);
        AssertSkillPrivacySurface(source);
    }

    [Fact]
    public void DashboardLinksSkillsOnlyInsideConfiguredProductContent()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "HeroPassport.Web",
            "Components",
            "Pages",
            "Home.razor"));

        var configuredBranch = source.IndexOf(
            "@if (_model.SetupCompleted && _model.Project is { } configuredProject",
            StringComparison.Ordinal);
        var skillsLink = source.IndexOf("href=\"/skills\"", StringComparison.Ordinal);

        Assert.True(configuredBranch >= 0, "Expected the existing configured dashboard branch.");
        Assert.True(skillsLink > configuredBranch, "Skills must only be linked from configured product content.");
        Assert.Equal(1, Count(source, "href=\"/skills\""));
    }

    [Fact]
    public void ProgramRegistersSkillServiceAndWebModelsRemainPersistencePrivate()
    {
        var root = FindRepositoryRoot();
        var program = File.ReadAllText(Path.Combine(root, "src", "HeroPassport.Web", "Program.cs"));
        var service = File.ReadAllText(Path.Combine(
            root,
            "src",
            "HeroPassport.Web",
            "Services",
            "HeroPassportSkillProgressionService.cs"));

        Assert.Contains("AddSingleton<HeroPassportSkillProgressionService>()", program, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.Data.Sqlite", service, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.EntityFrameworkCore", service, StringComparison.Ordinal);
        Assert.DoesNotContain("Infrastructure.Persistence", service, StringComparison.Ordinal);
        AssertSkillPrivacySurface(service);
    }

    private static string ReadPage(string fileName)
    {
        var root = FindRepositoryRoot();
        var path = Path.Combine(root, "src", "HeroPassport.Web", "Components", "Pages", fileName);
        Assert.True(File.Exists(path), $"Expected Skill progression page at '{path}'.");
        return File.ReadAllText(path);
    }

    private static void AssertGetOnlyStaticSsr(string source)
    {
        Assert.DoesNotContain("<EditForm", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SupplyParameterFromForm", source, StringComparison.Ordinal);
        Assert.DoesNotContain("method=\"post\"", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@rendermode", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("StreamRendering", source, StringComparison.Ordinal);
    }

    private static void AssertSkillPrivacySurface(string source)
    {
        foreach (var forbidden in new[]
        {
            "WorkspaceFingerprint",
            "ProjectId",
            "RequestId",
            "Receipt",
            "ArgsHash",
            "RuleVersion",
            "Sqlite",
            "InstallationSalt",
            "BootstrapCapability",
            "SessionCapability",
            "RemoteUrl",
            "FullPath",
        })
        {
            Assert.DoesNotContain(forbidden, source, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static int Count(string source, string value)
    {
        var count = 0;
        var offset = 0;
        while ((offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "HeroPassport.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not find HeroPassport.slnx from the test base directory.");
    }
}
