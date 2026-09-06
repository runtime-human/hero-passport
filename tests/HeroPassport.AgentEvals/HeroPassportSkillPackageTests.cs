using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace HeroPassport.AgentEvals;

public sealed class HeroPassportSkillPackageTests
{
    private static readonly string[] RequiredReferences =
    [
        "lifecycle.md",
        "finish-attestations.md",
        "recovery.md",
        "presentation.md",
    ];

    [Fact]
    public void SkillPackageMatchesPortableAgentSkillsFormatAndContractMetadata()
    {
        var skillRoot = SkillRoot();
        var skillPath = Path.Combine(skillRoot, "SKILL.md");
        Assert.True(File.Exists(skillPath), $"Missing required Agent Skills entrypoint: {skillPath}");

        var lines = File.ReadAllLines(skillPath);
        Assert.InRange(lines.Length, 1, 500);
        Assert.Equal("---", lines[0]);
        var frontmatterEnd = Array.FindIndex(lines, 1, static line => string.Equals(line.Trim(), "---", StringComparison.Ordinal));
        Assert.True(frontmatterEnd > 1, "SKILL.md must close its YAML frontmatter before Markdown instructions.");

        var nameLine = Assert.Single(lines[..frontmatterEnd], static line => line.StartsWith("name:", StringComparison.Ordinal));
        var name = nameLine["name:".Length..].Trim();
        Assert.InRange(name.Length, 1, 64);
        Assert.Matches(new Regex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant), name);
        Assert.Equal(Path.GetFileName(skillRoot), name);

        var description = Assert.Single(lines[..frontmatterEnd], static line => line.StartsWith("description:", StringComparison.Ordinal));
        var descriptionValue = description["description:".Length..].Trim();
        Assert.InRange(descriptionValue.Length, 1, 1024);
        Assert.Contains("project", descriptionValue, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("quest", descriptionValue, StringComparison.OrdinalIgnoreCase);

        var compatibility = Assert.Single(lines[..frontmatterEnd], static line => line.StartsWith("compatibility:", StringComparison.Ordinal));
        Assert.InRange(compatibility["compatibility:".Length..].Trim().Length, 1, 500);

        var text = File.ReadAllText(skillPath);
        Assert.Contains("hero-passport-skill/1", text, StringComparison.Ordinal);
        Assert.Contains("HP-MCP/2", text, StringComparison.Ordinal);

        foreach (var reference in RequiredReferences)
        {
            var referencePath = Path.Combine(skillRoot, "references", reference);
            Assert.True(File.Exists(referencePath), $"Missing required progressive-disclosure reference: {referencePath}");
            Assert.Contains($"references/{reference}", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void CoreInstructionsPreserveConservativeLifecycleRecoveryAndRetryPolicy()
    {
        var text = File.ReadAllText(Path.Combine(SkillRoot(), "SKILL.md"));

        Assert.Contains("hero.get_context", text, StringComparison.Ordinal);
        Assert.Contains("hero.start_quest", text, StringComparison.Ordinal);
        Assert.Contains("hero.finish_quest", text, StringComparison.Ordinal);
        Assert.Contains("autoStartQuest", text, StringComparison.Ordinal);
        Assert.Contains("autoFinishQuest", text, StringComparison.Ordinal);
        Assert.Contains("heroId", text, StringComparison.Ordinal);
        Assert.Contains("questId", text, StringComparison.Ordinal);
        Assert.Contains("startRequestId", text, StringComparison.Ordinal);
        Assert.Contains("finishRequestId", text, StringComparison.Ordinal);
        Assert.Contains("HP133", text, StringComparison.Ordinal);
        Assert.Contains("HP135", text, StringComparison.Ordinal);
        Assert.Contains("HP136", text, StringComparison.Ordinal);
        Assert.Contains("observed", text, StringComparison.Ordinal);
        Assert.Contains("reported", text, StringComparison.Ordinal);
        Assert.Contains("source", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("raw logs", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TriggerEvalCorpusIsBalancedRealisticAndStable()
    {
        var path = Path.Combine(RepoRoot(), "tests", "HeroPassport.AgentEvals", "eval_queries.json");
        Assert.True(File.Exists(path));

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var queries = document.RootElement.EnumerateArray().ToArray();
        Assert.Equal(20, queries.Length);

        var ids = queries.Select(static item => item.GetProperty("id").GetString()).ToArray();
        Assert.DoesNotContain(ids, static id => string.IsNullOrWhiteSpace(id));
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());

        Assert.Equal(10, queries.Count(static item => item.GetProperty("should_trigger").GetBoolean()));
        Assert.Equal(10, queries.Count(static item => !item.GetProperty("should_trigger").GetBoolean()));
        Assert.All(queries, static item => Assert.True(item.GetProperty("query").GetString()!.Length >= 20));
    }

    private static string SkillRoot() => Path.Combine(RepoRoot(), "skills", "hero-passport");

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HeroPassport.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Hero Passport repository root was not found from the AgentEvals output directory.");
    }
}
