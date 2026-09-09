using Xunit;

namespace HeroPassport.AgentEvals;

public sealed class ReleaseIntegrationDocumentationTests
{
    [Fact]
    public void ReleaseTimeHostInstructionsUseVerifiedCurrentSkillAndMcpMechanisms()
    {
        var integrations = Path.Combine(RepoRoot(), "docs", "integrations");

        AssertContainsAll(
            Path.Combine(integrations, "CODEX.md"),
            "Verified: 2026-09-09",
            ".agents/skills/hero-passport",
            "codex mcp add hero-passport -- hero-passport mcp",
            ".codex/config.toml",
            "codex mcp list");

        AssertContainsAll(
            Path.Combine(integrations, "CLAUDE-CODE.md"),
            "Verified: 2026-09-09",
            ".claude/skills/hero-passport",
            "claude mcp add --transport stdio --scope project hero-passport -- hero-passport mcp",
            "claude mcp list");

        AssertContainsAll(
            Path.Combine(integrations, "VSCODE.md"),
            "Verified: 2026-09-09",
            ".github/skills/hero-passport",
            "mcp.json",
            "\"type\": \"stdio\"",
            "${workspaceFolder}");

        AssertContainsAll(
            Path.Combine(integrations, "ZED.md"),
            "Verified: 2026-09-09",
            ".agents/skills/hero-passport",
            "\"context_servers\"",
            "\"command\": \"hero-passport\"");

        AssertContainsAll(
            Path.Combine(integrations, "JETBRAINS.md"),
            "Verified: 2026-09-09",
            "STDIO",
            "\"mcpServers\"",
            "\"command\": \"hero-passport\"",
            "\"args\": [\"mcp\"]");
    }

    private static void AssertContainsAll(string path, params string[] expected)
    {
        var text = File.ReadAllText(path);
        foreach (var value in expected)
        {
            Assert.Contains(value, text, StringComparison.Ordinal);
        }
    }

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
