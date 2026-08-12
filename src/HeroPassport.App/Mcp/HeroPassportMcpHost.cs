using HeroPassport.Application.Runtime;
using HeroPassport.Infrastructure.Persistence;
using HeroPassport.Infrastructure.ProjectIdentity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HeroPassport.App.Mcp;

public static class HeroPassportMcpHost
{
    public static async Task<int> RunAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var explicitProjectRoot = ParseProjectRoot(arguments);
        var dataHome = ResolveDataHome();
        Directory.CreateDirectory(dataHome);
        var databasePath = Path.Combine(dataHome, "hero-passport.db");

        await HeroPassportDatabase.InitializeAsync(databasePath, cancellationToken).ConfigureAwait(false);
        var salt = await HeroPassportDatabase.ReadProjectIdentitySaltAsync(databasePath, cancellationToken).ConfigureAwait(false);
        var identity = await ProjectIdentityResolver.ResolveAsync(
            explicitProjectRoot,
            Environment.CurrentDirectory,
            salt,
            cancellationToken).ConfigureAwait(false);

        var project = new ProjectBindingContext(identity.DisplayName, identity.WorkspaceFingerprint, identity.IdentityVersion);
        var application = new HeroPassportApplication(new SqliteHeroPassportStateStore(databasePath), TimeProvider.System);
        var endpoint = new HeroPassportMcpEndpoint(application, project);
        var tools = HeroPassportMcpToolCatalog.Create(endpoint);

        var builder = Host.CreateApplicationBuilder([]);
        builder.Logging.ClearProviders();
        builder.Services.AddMcpServer(options =>
            {
                options.ToolCollection = new OrderedMcpServerToolCollection(tools);
            })
            .WithStdioServerTransport();

        using var host = builder.Build();
        await host.RunAsync(cancellationToken).ConfigureAwait(false);
        return 0;
    }

    private static string? ParseProjectRoot(IReadOnlyList<string> arguments)
    {
        string? projectRoot = null;
        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            if (!string.Equals(argument, "--project-root", StringComparison.Ordinal))
            {
                throw new ArgumentException("Unsupported MCP command argument.", nameof(arguments));
            }

            if (projectRoot is not null || index + 1 >= arguments.Count || string.IsNullOrWhiteSpace(arguments[index + 1]))
            {
                throw new ArgumentException("--project-root requires exactly one non-empty value.", nameof(arguments));
            }

            projectRoot = arguments[++index];
        }

        return projectRoot;
    }

    private static string ResolveDataHome()
    {
        var explicitHome = Environment.GetEnvironmentVariable("HERO_PASSPORT_HOME");
        if (!string.IsNullOrWhiteSpace(explicitHome))
        {
            return Path.GetFullPath(explicitHome);
        }

        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HeroPassport");
        }

        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsMacOS())
        {
            return Path.Combine(userHome, "Library", "Application Support", "HeroPassport");
        }

        var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        return !string.IsNullOrWhiteSpace(xdgDataHome)
            ? Path.Combine(Path.GetFullPath(xdgDataHome), "HeroPassport")
            : Path.Combine(userHome, ".local", "share", "HeroPassport");
    }
}
