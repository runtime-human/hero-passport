using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Text.Json;
using Xunit;

namespace HeroPassport.App.Tests;

public sealed class McpStdioIntegrationTests
{
    [Fact]
    public async Task StdioServerListsCanonicalToolsAndReturnsDualStructuredBootstrapResult()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var root = FindRepositoryRoot();
        var sandbox = Path.Combine(Path.GetTempPath(), "hero-passport-mcp-tests", Guid.NewGuid().ToString("N"));
        var home = Path.Combine(sandbox, "home");
        var project = Path.Combine(sandbox, "project");
        Directory.CreateDirectory(home);
        Directory.CreateDirectory(project);

        try
        {
            var environment = StdioClientTransportOptions.GetDefaultEnvironmentVariables();
            environment["HERO_PASSPORT_HOME"] = home;
            var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
            if (dotnetRoot is not null)
            {
                environment["DOTNET_ROOT"] = dotnetRoot;
            }

            await using var client = await McpClient.CreateAsync(
                new StdioClientTransport(new StdioClientTransportOptions
                {
                    Name = "Hero Passport integration test",
                    Command = "dotnet",
                    Arguments =
                    [
                        "run",
                        "--no-build",
                        "-c",
                        "Release",
                        "--project",
                        Path.Combine(root, "src", "HeroPassport.App", "HeroPassport.App.csproj"),
                        "--",
                        "mcp",
                        "--project-root",
                        project,
                    ],
                    WorkingDirectory = project,
                    InheritEnvironmentVariables = false,
                    EnvironmentVariables = environment,
                }),
                cancellationToken: cancellationToken);

            var tools = await client.ListToolsAsync(cancellationToken: cancellationToken);
            Assert.Equal(
                [
                    "hero.bootstrap", "hero.configure", "hero.get_context", "hero.create", "hero.list", "hero.activate",
                    "hero.archive", "hero.restore", "hero.start_quest", "hero.finish_quest", "hero.get_card",
                ],
                tools.Select(static tool => tool.Name));

            var bootstrap = Assert.Single(tools, static tool => tool.Name == "hero.bootstrap");
            var result = await bootstrap.CallAsync(
                new Dictionary<string, object?>
                {
                    ["bootstrapRequestId"] = Guid.CreateVersion7().ToString("D"),
                    ["locale"] = "en-US",
                    ["heroName"] = "Nova",
                    ["presentationStyle"] = "rpg_engineering",
                    ["autoStartQuest"] = true,
                    ["autoFinishQuest"] = true,
                },
                cancellationToken: cancellationToken);

            Assert.NotEqual(true, result.IsError);
            Assert.NotNull(result.StructuredContent);
            var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
            using var textJson = JsonDocument.Parse(text.Text);
            Assert.True(JsonElement.DeepEquals(result.StructuredContent.Value, textJson.RootElement));
            Assert.True(result.StructuredContent.Value.GetProperty("setupCompleted").GetBoolean());
            Assert.Equal("Nova", result.StructuredContent.Value.GetProperty("hero").GetProperty("name").GetString());
        }
        finally
        {
            if (Directory.Exists(sandbox))
            {
                Directory.Delete(sandbox, recursive: true);
            }
        }
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

        throw new InvalidOperationException("Repository root could not be located for MCP integration tests.");
    }
}
