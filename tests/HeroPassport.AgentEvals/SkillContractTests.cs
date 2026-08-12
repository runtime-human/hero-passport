using System.Text.RegularExpressions;
using Xunit;

namespace HeroPassport.AgentEvals;

public sealed partial class SkillContractTests
{
    [Fact]
    public void SkillPackageMatchesAgentSkillsAndHeroPassportContracts()
    {
        var root = FindRepositoryRoot();
        var skillRoot = Path.Combine(root, "skills", "hero-passport");
        var skillPath = Path.Combine(skillRoot, "SKILL.md");
        var content = File.ReadAllText(skillPath);

        Assert.StartsWith("---\n", content, StringComparison.Ordinal);
        Assert.Contains("\nname: hero-passport\n", content, StringComparison.Ordinal);
        Assert.Contains("\ndescription:", content, StringComparison.Ordinal);
        Assert.Contains("hero-passport-skill-contract: \"hero-passport-skill/1\"", content, StringComparison.Ordinal);
        Assert.Contains("hero-passport-mcp-contract: \"HP-MCP/2\"", content, StringComparison.Ordinal);
        Assert.Matches(FrontmatterNameRegex(), content);

        string[] requiredInstructions =
        [
            "hero.get_context",
            "autoStartQuest",
            "startRequestId",
            "finishRequestId",
            "HP133",
            "HP135",
            "HP136",
            "reported",
            "observed",
            "Hero Passport calls themselves never justify the `tool_use` Skill",
        ];
        foreach (var instruction in requiredInstructions)
        {
            Assert.Contains(instruction, content, StringComparison.Ordinal);
        }

        string[] references = ["lifecycle.md", "finish-attestations.md", "recovery.md", "presentation.md"];
        foreach (var reference in references)
        {
            Assert.True(File.Exists(Path.Combine(skillRoot, "references", reference)), $"Missing Skill reference: {reference}");
        }
    }

    [Fact]
    public void SkillActivationDescriptionContainsPositiveAndNegativeTriggerGuidance()
    {
        var content = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "skills", "hero-passport", "SKILL.md"));
        var end = content.IndexOf("\n---\n", 4, StringComparison.Ordinal);
        Assert.True(end > 0);
        var frontmatter = content[..(end + 5)];

        Assert.Contains("implementation", frontmatter, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("debugging", frontmatter, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("project", frontmatter, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not activate", frontmatter, StringComparison.Ordinal);
        Assert.Contains("casual conversation", frontmatter, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("short factual questions", frontmatter, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepositoryRoot()
    {
        foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "HeroPassport.slnx")))
                {
                    return directory.FullName;
                }
            }
        }

        throw new InvalidOperationException("Repository root could not be located for Skill evals.");
    }

    [GeneratedRegex("(?m)^name: hero-passport$")]
    private static partial Regex FrontmatterNameRegex();
}
