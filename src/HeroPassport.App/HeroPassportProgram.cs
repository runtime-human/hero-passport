using HeroPassport.App.Mcp;
using HeroPassport.Application.Runtime;
using HeroPassport.Infrastructure.Persistence;
using HeroPassport.Infrastructure.ProjectIdentity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace HeroPassport.App;

public static class HeroPassportProgram
{
    private static readonly Dictionary<string, int> McpToolOrder = HpMcpToolCatalog.ProtocolTools
        .Select(static (tool, index) => new KeyValuePair<string, int>(tool.Name, index))
        .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);

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
            if (args.Length == 0 || !string.Equals(args[0], "mcp", StringComparison.Ordinal))
            {
                Console.Error.WriteLine("Usage: hero-passport mcp [--project-root <directory>]");
                return 2;
            }

            var explicitProjectRoot = ParseProjectRoot(args);
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

    private static string? ParseProjectRoot(string[] args)
    {
        if (args.Length == 1)
        {
            return null;
        }

        if (args.Length == 3 && string.Equals(args[1], "--project-root", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(args[2]))
        {
            return args[2];
        }

        throw new ProjectIdentityException("HP310", "Project binding is invalid.");
    }
}
