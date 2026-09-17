using Xunit;

namespace HeroPassport.Web.Tests;

public sealed class HistoryStaticSsrTests
{
    [Fact]
    public void HistoryListIsAuthenticatedGetOnlyStaticSsrSurface()
    {
        var source = ReadPage("History.razor");

        Assert.Contains("@page \"/history\"", source, StringComparison.Ordinal);
        Assert.Contains("@inject HeroPassportHistoryService HistoryFlow", source, StringComparison.Ordinal);
        Assert.Contains("LoadListAsync", source, StringComparison.Ordinal);
        Assert.Contains("Latest 25", source, StringComparison.Ordinal);
        Assert.Contains("href=\"/history/@item.QuestId\"", source, StringComparison.Ordinal);
        Assert.Contains("href=\"/\"", source, StringComparison.Ordinal);
        AssertGetOnlyStaticSsr(source);
        AssertHistoryPrivacySurface(source);
    }

    [Fact]
    public void HistoryDetailUsesCanonicalServiceStatusMappingAndCascadingHttpContext()
    {
        var source = ReadPage("QuestHistory.razor");

        Assert.Contains("@page \"/history/{QuestId}\"", source, StringComparison.Ordinal);
        Assert.Contains("@inject HeroPassportHistoryService HistoryFlow", source, StringComparison.Ordinal);
        Assert.Contains("[Parameter]", source, StringComparison.Ordinal);
        Assert.Contains("public string QuestId", source, StringComparison.Ordinal);
        Assert.Contains("[CascadingParameter]", source, StringComparison.Ordinal);
        Assert.Contains("HttpContext? HttpContext", source, StringComparison.Ordinal);
        Assert.Contains("LoadDetailAsync(QuestId)", source, StringComparison.Ordinal);
        Assert.Contains("QuestHistoryPageStatus.Invalid", source, StringComparison.Ordinal);
        Assert.Contains("Status400BadRequest", source, StringComparison.Ordinal);
        Assert.Contains("QuestHistoryPageStatus.NotFound", source, StringComparison.Ordinal);
        Assert.Contains("Status404NotFound", source, StringComparison.Ordinal);
        Assert.Contains("QuestHistoryPageStatus.SetupRequired", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IHttpContextAccessor", source, StringComparison.Ordinal);
        AssertGetOnlyStaticSsr(source);
        AssertHistoryPrivacySurface(source);
    }

    [Fact]
    public void DashboardLinksHistoryOnlyInsideConfiguredProductContent()
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
        var historyLink = source.IndexOf("href=\"/history\"", StringComparison.Ordinal);

        Assert.True(configuredBranch >= 0, "Expected the existing configured dashboard branch.");
        Assert.True(historyLink > configuredBranch, "History must only be linked from configured product content.");
        Assert.Equal(1, Count(source, "href=\"/history\""));
    }

    [Fact]
    public void ProgramRegistersHistoryServiceAndWebModelsRemainPersistencePrivate()
    {
        var root = FindRepositoryRoot();
        var program = File.ReadAllText(Path.Combine(root, "src", "HeroPassport.Web", "Program.cs"));
        var service = File.ReadAllText(Path.Combine(
            root,
            "src",
            "HeroPassport.Web",
            "Services",
            "HeroPassportHistoryService.cs"));

        Assert.Contains("AddSingleton<HeroPassportHistoryService>()", program, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.Data.Sqlite", service, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.EntityFrameworkCore", service, StringComparison.Ordinal);
        Assert.DoesNotContain("Infrastructure.Persistence", service, StringComparison.Ordinal);
        AssertHistoryPrivacySurface(service);
    }

    private static string ReadPage(string fileName)
    {
        var root = FindRepositoryRoot();
        var path = Path.Combine(root, "src", "HeroPassport.Web", "Components", "Pages", fileName);
        Assert.True(File.Exists(path), $"Expected history page at '{path}'.");
        return File.ReadAllText(path);
    }

    private static void AssertGetOnlyStaticSsr(string source)
    {
        Assert.DoesNotContain("<EditForm", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SupplyParameterFromForm", source, StringComparison.Ordinal);
        Assert.DoesNotContain("method=\"post\"", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@rendermode", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("StreamRendering", source, StringComparison.Ordinal);
        Assert.DoesNotContain("NavigationManager", source, StringComparison.Ordinal);
    }

    private static void AssertHistoryPrivacySurface(string source)
    {
        foreach (var forbidden in new[]
        {
            "WorkspaceFingerprint",
            "ProjectId",
            "RequestId",
            "Receipt",
            "ArgsHash",
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
