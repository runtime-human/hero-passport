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
        Assert.DoesNotContain("FinishRequestId", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HeroId", source, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkspaceFingerprint", source, StringComparison.Ordinal);
        Assert.DoesNotContain("type=\"hidden\" name=\"Summary\"", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("type=\"hidden\" name=\"SkillsUsed\"", source, StringComparison.OrdinalIgnoreCase);
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
