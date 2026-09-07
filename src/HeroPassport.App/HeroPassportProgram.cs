using HeroPassport.App.Mcp;
using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using HeroPassport.Infrastructure.Persistence;
using HeroPassport.Infrastructure.ProjectIdentity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.CommandLine;
using System.Text.Json;

namespace HeroPassport.App;

public static class HeroPassportProgram
{
    private static readonly Dictionary<string, int> McpToolOrder = HpMcpToolCatalog.ProtocolTools
        .Select(static (tool, index) => new KeyValuePair<string, int>(tool.Name, index))
        .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);

    private static readonly JsonSerializerOptions CliJsonOptions = new(JsonSerializerDefaults.Web);

    private const string McpServerInstructions =
        "Use the installed Hero Passport Agent Skill for ambient lifecycle policy.\n" +
        "Call hero.get_context to hydrate/recover uncertain state.\n" +
        "Pass explicit heroId when starting a Quest and carry returned questId.\n" +
        "Reuse mutation request IDs only for retries of the same canonical intent.\n" +
        "Never send source, diffs, raw logs, prompts, secrets, environment dumps or workspace paths.";

    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        try
        {
            var rootCommand = CreateRootCommand();
            return await rootCommand.Parse(args).InvokeAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (HeroPassportException exception)
        {
            Console.Error.WriteLine($"{exception.Code}: {exception.Message}");
            return 2;
        }
        catch (ProjectIdentityException exception)
        {
            Console.Error.WriteLine($"{exception.Code}: {exception.Message}");
            return 2;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return 0;
        }
        catch
        {
            Console.Error.WriteLine("Hero Passport failed to start.");
            return 1;
        }
    }

    private static RootCommand CreateRootCommand()
    {
        var rootCommand = new RootCommand("Local RPG passport for AI coding agents.");

        var projectRootOption = new Option<string?>("--project-root")
        {
            Description = "Explicit project root used for project-identity/1 binding.",
        };
        var mcpCommand = new Command("mcp", "Run the local stdio HP-MCP/2 server.")
        {
            projectRootOption,
        };
        mcpCommand.SetAction((parseResult, token) =>
            RunMcpAsync(parseResult.GetValue(projectRootOption), token));
        rootCommand.Subcommands.Add(mcpCommand);

        var localeOption = new Option<string>("--locale")
        {
            Description = "Initial locale: ru-RU or en-US.",
            Required = true,
        };
        localeOption.AcceptOnlyFromAmong("ru-RU", "en-US");

        var heroNameOption = new Option<string>("--hero-name")
        {
            Description = "Initial Hero name.",
            Required = true,
        };

        var presentationStyleOption = new Option<string>("--presentation-style")
        {
            Description = "Initial presentation style.",
            DefaultValueFactory = static _ => "rpg_engineering",
        };
        presentationStyleOption.AcceptOnlyFromAmong("rpg_engineering", "classic_rpg", "minimal");

        var autoStartQuestOption = new Option<bool>("--auto-start-quest")
        {
            Description = "Whether the Agent Skill should automatically start appropriate Quests.",
            DefaultValueFactory = static _ => true,
        };
        var autoFinishQuestOption = new Option<bool>("--auto-finish-quest")
        {
            Description = "Whether the Agent Skill should automatically finish appropriate Quests.",
            DefaultValueFactory = static _ => true,
        };
        var requestIdOption = new Option<string?>("--request-id")
        {
            Description = "Optional lowercase canonical UUIDv7 bootstrap request identity for retry-safe scripts.",
        };
        var jsonOption = new Option<bool>("--json")
        {
            Description = "Write one machine-readable JSON result to stdout.",
        };

        var initCommand = new Command("init", "Initialize Hero Passport and create the first Hero.")
        {
            localeOption,
            heroNameOption,
            presentationStyleOption,
            autoStartQuestOption,
            autoFinishQuestOption,
            requestIdOption,
            jsonOption,
        };
        initCommand.SetAction((parseResult, token) => RunInitAsync(
            parseResult.GetValue(localeOption)!,
            parseResult.GetValue(heroNameOption)!,
            parseResult.GetValue(presentationStyleOption)!,
            parseResult.GetValue(autoStartQuestOption),
            parseResult.GetValue(autoFinishQuestOption),
            parseResult.GetValue(requestIdOption),
            parseResult.GetValue(jsonOption),
            token));
        rootCommand.Subcommands.Add(initCommand);

        return rootCommand;
    }

    private static async Task<int> RunInitAsync(
        string locale,
        string heroName,
        string presentationStyle,
        bool autoStartQuest,
        bool autoFinishQuest,
        string? requestId,
        bool json,
        CancellationToken cancellationToken)
    {
        var bootstrapRequestId = ParseOrCreateRequestId(requestId);
        var databasePath = HeroPassportRuntimePaths.ResolveDatabasePath();
        await HeroPassportDatabase.InitializeAsync(databasePath, cancellationToken).ConfigureAwait(false);

        var application = new HeroPassportApplication(new SqliteHeroPassportStateStore(databasePath), TimeProvider.System);
        var result = await application.BootstrapAsync(
            new BootstrapRequest(
                bootstrapRequestId,
                locale,
                heroName,
                presentationStyle,
                autoStartQuest,
                autoFinishQuest),
            cancellationToken).ConfigureAwait(false);

        if (json)
        {
            var payload = new
            {
                bootstrapRequestId = bootstrapRequestId.ToString(),
                hero = new
                {
                    heroId = result.Hero.HeroId.ToString(),
                    name = result.Hero.Name,
                },
                settings = new
                {
                    locale = result.Settings.Locale,
                    presentationStyle = result.Settings.PresentationStyle,
                    autoStartQuest = result.Settings.AutoStartQuest,
                    autoFinishQuest = result.Settings.AutoFinishQuest,
                },
                replayed = result.Replayed,
            };
            Console.Out.WriteLine(JsonSerializer.Serialize(payload, CliJsonOptions));
            return 0;
        }

        Console.Out.WriteLine(result.Replayed ? "Hero Passport bootstrap replayed." : "Hero Passport initialized.");
        Console.Out.WriteLine($"Hero: {result.Hero.Name}");
        Console.Out.WriteLine($"Hero ID: {result.Hero.HeroId}");
        Console.Out.WriteLine($"Locale: {result.Settings.Locale}");
        Console.Out.WriteLine($"Presentation: {result.Settings.PresentationStyle}");
        return 0;
    }

    private static MutationRequestId ParseOrCreateRequestId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return MutationRequestId.New();
        }

        try
        {
            return MutationRequestId.Parse(value);
        }
        catch (FormatException)
        {
            throw new HeroPassportException("HP300", "Invalid requestId.");
        }
    }

    private static async Task<int> RunMcpAsync(string? explicitProjectRoot, CancellationToken cancellationToken)
    {
        var databasePath = HeroPassportRuntimePaths.ResolveDatabasePath();
        await HeroPassportDatabase.InitializeAsync(databasePath, cancellationToken).ConfigureAwait(false);
        var salt = await HeroPassportDatabase.ReadProjectIdentitySaltAsync(databasePath, cancellationToken).ConfigureAwait(false);
        var resolved = await ProjectIdentityResolver.ResolveAsync(
            explicitProjectRoot,
            Directory.GetCurrentDirectory(),
            salt,
            cancellationToken).ConfigureAwait(false);
        var project = new ProjectBindingContext(resolved.DisplayName, resolved.WorkspaceFingerprint, resolved.IdentityVersion);

        var application = new HeroPassportApplication(new SqliteHeroPassportStateStore(databasePath), TimeProvider.System);
        var adapter = new HpMcpAdapter(application, _ => Task.FromResult(project));

        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Services
            .AddMcpServer(options => options.ServerInstructions = McpServerInstructions)
            .WithStdioServerTransport()
            .WithRequestFilters(filters =>
            {
                filters.AddListToolsFilter(next => async (request, token) =>
                {
                    var result = await next(request, token).ConfigureAwait(false);
                    result.Tools = result.Tools
                        .OrderBy(static tool => McpToolOrder.TryGetValue(tool.Name, out var order) ? order : int.MaxValue)
                        .ThenBy(static tool => tool.Name, StringComparer.Ordinal)
                        .ToArray();
                    return result;
                });
            })
            .WithTools(HpMcpServerTools.Create(adapter));

        await builder.Build().RunAsync(cancellationToken).ConfigureAwait(false);
        return 0;
    }
}
