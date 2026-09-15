using Xunit;

namespace HeroPassport.Web.Tests;

public sealed class FinishQuestStaticSsrTests
{
    [Fact]
    public void FinishPreparePageUsesDedicatedBoundedFormContract()
    {
        var root = FindRepositoryRoot();
        var path = Path.Combine(root, "src", "HeroPassport.Web", "Components", "Pages", "FinishQuest.razor");
        Assert.True(File.Exists(path), $"Expected Finish Quest page at '{path}'.");

        var source = File.ReadAllText(path);
        Assert.Contains("@page \"/quests/finish/{QuestId}\"", source, StringComparison.Ordinal);
        Assert.Equal(1, Count(source, "FormName=\"FinishQuestPrepare\""));
        Assert.Equal(1, Count(source, "SupplyParameterFromForm(FormName = \"FinishQuestPrepare\")"));
        Assert.Contains("FinishQuestForm", source, StringComparison.Ordinal);
        Assert.Contains("<InputTextArea @bind-Value=\"Input!.Summary\" maxlength=\"4000\" />", source, StringComparison.Ordinal);
        Assert.DoesNotContain("maxlength=\"2000\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("FinishRequestId", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HeroId", source, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkspaceFingerprint", source, StringComparison.Ordinal);
        Assert.DoesNotContain("type=\"hidden\" name=\"Summary\"", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("type=\"hidden\" name=\"SkillsUsed\"", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FinishPrepareFormRemainsRegisteredBeforeUnavailableStateBranch()
    {
        var root = FindRepositoryRoot();
        var path = Path.Combine(root, "src", "HeroPassport.Web", "Components", "Pages", "FinishQuest.razor");
        var source = File.ReadAllText(path);

        var formIndex = source.IndexOf(
            "<EditForm Model=\"Input\" FormName=\"FinishQuestPrepare\"",
            StringComparison.Ordinal);
        var unavailableBranchIndex = source.IndexOf(
            "_load.Status is FinishQuestPageStatus.Invalid or FinishQuestPageStatus.NotFound",
            StringComparison.Ordinal);

        Assert.True(formIndex >= 0, "FinishQuestPrepare form must remain registered for static-SSR POST routing.");
        Assert.True(unavailableBranchIndex >= 0, "Expected bounded unavailable-state branch.");
        Assert.True(
            formIndex < unavailableBranchIndex,
            "FinishQuestPrepare must be registered before the unavailable-state branch so a stale POST can still dispatch to PrepareAsync.");
    }

    [Fact]
    public void FinishConfirmationPostsOnlyOpaqueRouteHandleAndFrameworkMetadata()
    {
        var root = FindRepositoryRoot();
        var path = Path.Combine(root, "src", "HeroPassport.Web", "Components", "Pages", "ConfirmFinishQuest.razor");
        Assert.True(File.Exists(path), $"Expected Finish confirmation page at '{path}'.");

        var source = File.ReadAllText(path);
        Assert.Contains("@page \"/quests/finish/confirm/{Handle}\"", source, StringComparison.Ordinal);
        Assert.Equal(1, Count(source, "FormName=\"FinishQuestConfirm\""));
        Assert.Equal(1, Count(source, "SupplyParameterFromForm(FormName = \"FinishQuestConfirm\")"));
        Assert.DoesNotContain("type=\"hidden\"", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("FinishRequestId", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HeroId", source, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkspaceFingerprint", source, StringComparison.Ordinal);
        Assert.DoesNotContain("args_hash", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FinishConfirmFormRemainsRegisteredBeforeGoneAndBusyStateBranches()
    {
        var root = FindRepositoryRoot();
        var path = Path.Combine(root, "src", "HeroPassport.Web", "Components", "Pages", "ConfirmFinishQuest.razor");
        var source = File.ReadAllText(path);

        var formIndex = source.IndexOf(
            "<EditForm Model=\"ConfirmationInput\" FormName=\"FinishQuestConfirm\"",
            StringComparison.Ordinal);
        var goneBranchIndex = source.IndexOf(
            "_result.Status is ConfirmFinishQuestWebStatus.Invalid or ConfirmFinishQuestWebStatus.Gone",
            StringComparison.Ordinal);
        var busyBranchIndex = source.IndexOf(
            "_result.Status == ConfirmFinishQuestWebStatus.Busy",
            StringComparison.Ordinal);

        Assert.True(formIndex >= 0, "FinishQuestConfirm form must remain registered for static-SSR POST routing.");
        Assert.True(goneBranchIndex >= 0, "Expected bounded invalid/gone state branch.");
        Assert.True(busyBranchIndex >= 0, "Expected bounded busy state branch.");
        Assert.True(
            formIndex < goneBranchIndex && formIndex < busyBranchIndex,
            "FinishQuestConfirm must be registered before retry-terminal/busy branches so stale or concurrent POSTs can dispatch to ConfirmAsync.");
    }

    [Fact]
    public void ProgramRegistersProcessLocalFinishQuestServices()
    {
        var root = FindRepositoryRoot();
        var program = File.ReadAllText(Path.Combine(root, "src", "HeroPassport.Web", "Program.cs"));

        Assert.Contains("AddSingleton<PendingFinishQuestStore>()", program, StringComparison.Ordinal);
        Assert.Contains("AddSingleton<HeroPassportFinishQuestService>()", program, StringComparison.Ordinal);
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
