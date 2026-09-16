using Xunit;

namespace HeroPassport.Web.Tests;

public sealed class StartQuestStaticSsrTests
{
    [Fact]
    public void StartQuestPageUsesDedicatedStaticSsrFormContract()
    {
        var root = FindRepositoryRoot();
        var path = Path.Combine(root, "src", "HeroPassport.Web", "Components", "Pages", "StartQuest.razor");
        Assert.True(File.Exists(path), $"Expected Start Quest page at '{path}'.");

        var source = File.ReadAllText(path);
        Assert.Contains("@page \"/quests/start\"", source, StringComparison.Ordinal);
        Assert.Equal(1, Count(source, "FormName=\"StartQuestPrepare\""));
        Assert.Equal(1, Count(source, "SupplyParameterFromForm(FormName = \"StartQuestPrepare\")"));
        Assert.Contains("StartQuestForm", source, StringComparison.Ordinal);
        Assert.DoesNotContain("type=\"hidden\" name=\"Title\"", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("type=\"hidden\" name=\"Goal\"", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("HeroId", source, StringComparison.Ordinal);
        Assert.DoesNotContain("StartRequestId", source, StringComparison.Ordinal);
    }

    [Fact]
    public void StartQuestPrepareFormRemainsRegisteredBeforeUnavailableStateBranch()
    {
        var root = FindRepositoryRoot();
        var path = Path.Combine(root, "src", "HeroPassport.Web", "Components", "Pages", "StartQuest.razor");
        var source = File.ReadAllText(path);

        var formIndex = source.IndexOf(
            "<EditForm Model=\"Input\" FormName=\"StartQuestPrepare\"",
            StringComparison.Ordinal);
        var unavailableBranchIndex = source.IndexOf(
            "else if (!_page.Available)",
            StringComparison.Ordinal);

        Assert.True(formIndex >= 0, "StartQuestPrepare form must remain registered for static-SSR POST routing.");
        Assert.True(unavailableBranchIndex >= 0, "Expected bounded unavailable-state branch.");
        Assert.True(
            formIndex < unavailableBranchIndex,
            "StartQuestPrepare must be registered before the unavailable-state branch so a stale POST can still dispatch to PrepareAsync.");
    }

    [Fact]
    public void ConfirmationPagePostsOnlyOpaqueRouteHandleAndFrameworkMetadata()
    {
        var root = FindRepositoryRoot();
        var path = Path.Combine(root, "src", "HeroPassport.Web", "Components", "Pages", "ConfirmStartQuest.razor");
        Assert.True(File.Exists(path), $"Expected confirmation page at '{path}'.");

        var source = File.ReadAllText(path);
        Assert.Contains("@page \"/quests/start/confirm/{Handle}\"", source, StringComparison.Ordinal);
        Assert.Contains("FormName=\"StartQuestConfirm\"", source, StringComparison.Ordinal);
        Assert.Contains("SupplyParameterFromForm(FormName = \"StartQuestConfirm\")", source, StringComparison.Ordinal);
        Assert.DoesNotContain("type=\"hidden\"", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("HeroId", source, StringComparison.Ordinal);
        Assert.DoesNotContain("StartRequestId", source, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkspaceFingerprint", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ProgramRegistersProcessLocalStartQuestServices()
    {
        var root = FindRepositoryRoot();
        var program = File.ReadAllText(Path.Combine(root, "src", "HeroPassport.Web", "Program.cs"));

        Assert.Contains("AddSingleton(TimeProvider.System)", program, StringComparison.Ordinal);
        Assert.Contains("AddSingleton<PendingStartQuestStore>()", program, StringComparison.Ordinal);
        Assert.Contains("AddSingleton<HeroPassportStartQuestService>()", program, StringComparison.Ordinal);
    }

    [Fact]
    public void DashboardExposesStartAndExplicitOpenQuestFinishEntryPoints()
    {
        var root = FindRepositoryRoot();
        var home = File.ReadAllText(Path.Combine(root, "src", "HeroPassport.Web", "Components", "Pages", "Home.razor"));

        Assert.Contains("href=\"/quests/start\"", home, StringComparison.Ordinal);
        Assert.Contains("Start Quest", home, StringComparison.Ordinal);
        Assert.Contains("/quests/finish/{quest.QuestId}", home, StringComparison.Ordinal);
        Assert.Contains("Finish Quest", home, StringComparison.Ordinal);
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
