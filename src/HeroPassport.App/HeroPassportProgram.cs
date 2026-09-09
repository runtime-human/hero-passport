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
using System.Globalization;
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
            var parseResult = rootCommand.Parse(args);
            var invocationConfiguration = new InvocationConfiguration
            {
                EnableDefaultExceptionHandler = false,
                Error = Console.Error,
                Output = parseResult.Errors.Count == 0 ? Console.Out : Console.Error,
            };
            return await parseResult
                .InvokeAsync(invocationConfiguration, cancellationToken)
                .ConfigureAwait(false);
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

        var doctorJsonOption = new Option<bool>("--json")
        {
            Description = "Write one machine-readable diagnostic report to stdout.",
        };
        var doctorCommand = new Command(
            "doctor",
            "Inspect Hero Passport SQLite policy, migrations, lock state and integrity without modifying the database.")
        {
            doctorJsonOption,
        };
        doctorCommand.SetAction((parseResult, token) =>
            RunDoctorAsync(parseResult.GetValue(doctorJsonOption), token));
        rootCommand.Subcommands.Add(doctorCommand);

        var confirmProcessesStoppedOption = new Option<bool>("--confirm-processes-stopped")
        {
            Description = "Explicitly confirm that all competing Hero Passport processes have been stopped before migration-lock repair.",
            Required = true,
        };
        var repairJsonOption = new Option<bool>("--json")
        {
            Description = "Write one machine-readable repair result to stdout.",
        };
        var migrationLockCommand = new Command(
            "migration-lock",
            "Explicitly clear an abandoned EF SQLite migration lock after safety and integrity checks.")
        {
            confirmProcessesStoppedOption,
            repairJsonOption,
        };
        migrationLockCommand.SetAction((parseResult, token) => RunMigrationLockRepairAsync(
            parseResult.GetValue(confirmProcessesStoppedOption),
            parseResult.GetValue(repairJsonOption),
            token));
        var repairCommand = new Command("repair", "Explicit storage repair commands.")
        {
            migrationLockCommand,
        };
        rootCommand.Subcommands.Add(repairCommand);

        var rebuildJsonOption = new Option<bool>("--json")
        {
            Description = "Write one machine-readable projection rebuild result to stdout.",
        };
        var projectionsCommand = new Command(
            "projections",
            "Rebuild mutable Hero Passport projections from persisted canonical history.")
        {
            rebuildJsonOption,
        };
        projectionsCommand.SetAction((parseResult, token) =>
            RunProjectionRebuildAsync(parseResult.GetValue(rebuildJsonOption), token));
        var rebuildCommand = new Command("rebuild", "Explicit rebuild commands.")
        {
            projectionsCommand,
        };
        rootCommand.Subcommands.Add(rebuildCommand);

        var backupOutputOption = new Option<string>("--output")
        {
            Description = "New destination path for the validated SQLite backup. Existing files are never overwritten.",
            Required = true,
        };
        var backupJsonOption = new Option<bool>("--json")
        {
            Description = "Write one machine-readable backup result to stdout.",
        };
        var backupCommand = new Command(
            "backup",
            "Create and validate an online SQLite backup without raw-copying the active WAL database.")
        {
            backupOutputOption,
            backupJsonOption,
        };
        backupCommand.SetAction((parseResult, token) => RunBackupAsync(
            parseResult.GetValue(backupOutputOption)!,
            parseResult.GetValue(backupJsonOption),
            token));
        rootCommand.Subcommands.Add(backupCommand);

        var exportOutputOption = new Option<string>("--output")
        {
            Description = "New destination path for the privacy-bounded JSON export. Existing files are never overwritten.",
            Required = true,
        };
        var exportJsonOption = new Option<bool>("--json")
        {
            Description = "Write one machine-readable export result to stdout.",
        };
        var exportCommand = new Command(
            "export",
            "Create a user-facing RPG/Quest JSON snapshot without private persistence metadata.")
        {
            exportOutputOption,
            exportJsonOption,
        };
        exportCommand.SetAction((parseResult, token) => RunExportAsync(
            parseResult.GetValue(exportOutputOption)!,
            parseResult.GetValue(exportJsonOption),
            token));
        rootCommand.Subcommands.Add(exportCommand);

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

        var deleteHeroIdOption = new Option<string>("--hero-id")
        {
            Description = "Canonical lowercase UUIDv7 of the Hero to delete.",
            Required = true,
        };
        var confirmLogicalDeleteOption = new Option<bool>("--confirm-logical-delete")
        {
            Description = "Explicitly confirm permanent logical deletion from Hero Passport application state; this is not forensic erasure.",
            Required = true,
        };
        var deleteJsonOption = new Option<bool>("--json")
        {
            Description = "Write one machine-readable JSON result to stdout.",
        };
        var deleteCommand = new Command(
            "delete",
            "Permanently remove a non-active Hero from application state. Logical deletion only; not forensic erasure.")
        {
            deleteHeroIdOption,
            confirmLogicalDeleteOption,
            deleteJsonOption,
        };
        deleteCommand.SetAction((parseResult, token) => RunDeleteHeroAsync(
            parseResult.GetValue(deleteHeroIdOption)!,
            parseResult.GetValue(confirmLogicalDeleteOption),
            parseResult.GetValue(deleteJsonOption),
            token));

        var heroCommand = new Command("hero", "Hero administration commands.")
        {
            deleteCommand,
        };
        rootCommand.Subcommands.Add(heroCommand);

        return rootCommand;
    }

    private static async Task<int> RunDoctorAsync(bool json, CancellationToken cancellationToken)
    {
        var databasePath = HeroPassportRuntimePaths.ResolveDatabasePath();
        var report = await HeroPassportDatabaseDoctor
            .InspectAsync(databasePath, cancellationToken)
            .ConfigureAwait(false);

        if (json)
        {
            Console.Out.WriteLine(JsonSerializer.Serialize(report, CliJsonOptions));
            return report.Healthy ? 0 : 1;
        }

        Console.Out.WriteLine($"Database: {(report.DatabaseExists ? "present" : "not initialized")}");
        Console.Out.WriteLine($"Storage: {report.StorageLocationKind} ({report.StorageDriveType}, supported: {report.StorageLocationSupported})");
        Console.Out.WriteLine($"SQLite: {report.SqliteVersion ?? "unavailable"} (supported: {report.SqliteVersionSupported})");
        Console.Out.WriteLine($"Journal mode: {report.JournalMode ?? "unavailable"}");
        Console.Out.WriteLine($"Synchronous: {report.Synchronous?.ToString(CultureInfo.InvariantCulture) ?? "unavailable"}");
        Console.Out.WriteLine($"Foreign keys: {report.ForeignKeys?.ToString() ?? "unavailable"}");
        Console.Out.WriteLine($"Trusted schema: {report.TrustedSchema?.ToString() ?? "unavailable"}");
        Console.Out.WriteLine($"Migrations: {report.MigrationState}");
        Console.Out.WriteLine($"Migration lock suspected: {report.MigrationLockSuspected}");
        Console.Out.WriteLine($"Quick check: {(report.QuickCheckPassed ? "ok" : "failed")}");
        Console.Out.WriteLine($"Foreign key violations: {report.ForeignKeyViolationCount}");
        Console.Out.WriteLine($"Healthy: {report.Healthy}");
        return report.Healthy ? 0 : 1;
    }

    private static async Task<int> RunMigrationLockRepairAsync(
        bool confirmedProcessesStopped,
        bool json,
        CancellationToken cancellationToken)
    {
        if (!confirmedProcessesStopped)
        {
            throw new HeroPassportException("HP300", "Option --confirm-processes-stopped is required.");
        }

        var databasePath = HeroPassportRuntimePaths.ResolveDatabasePath();
        var result = await HeroPassportMigrationLockRepair
            .RepairAsync(databasePath, competingProcessesStopped: true, cancellationToken)
            .ConfigureAwait(false);

        if (json)
        {
            var payload = new
            {
                result.LockCleared,
                beforeMigrationLockSuspected = result.Before.MigrationLockSuspected,
                afterMigrationLockSuspected = result.After.MigrationLockSuspected,
                migrationState = result.After.MigrationState,
                quickCheckPassed = result.After.QuickCheckPassed,
                foreignKeyViolationCount = result.After.ForeignKeyViolationCount,
                healthy = result.After.Healthy,
            };
            Console.Out.WriteLine(JsonSerializer.Serialize(payload, CliJsonOptions));
            return result.After.Healthy ? 0 : 1;
        }

        Console.Out.WriteLine($"Migration lock cleared: {result.LockCleared}");
        Console.Out.WriteLine($"Migration state: {result.After.MigrationState}");
        Console.Out.WriteLine($"Quick check: {(result.After.QuickCheckPassed ? "ok" : "failed")}");
        Console.Out.WriteLine($"Foreign key violations: {result.After.ForeignKeyViolationCount}");
        Console.Out.WriteLine($"Healthy: {result.After.Healthy}");
        return result.After.Healthy ? 0 : 1;
    }

    private static async Task<int> RunProjectionRebuildAsync(
        bool json,
        CancellationToken cancellationToken)
    {
        var databasePath = HeroPassportRuntimePaths.ResolveDatabasePath();
        var result = await HeroPassportProjectionRebuilder
            .RebuildAsync(databasePath, cancellationToken)
            .ConfigureAwait(false);

        if (json)
        {
            var payload = new
            {
                result.HeroesRebuilt,
                result.HeroSkillsRebuilt,
                result.HeroProjectStatsRebuilt,
                healthy = result.After.Healthy,
            };
            Console.Out.WriteLine(JsonSerializer.Serialize(payload, CliJsonOptions));
            return result.After.Healthy ? 0 : 1;
        }

        Console.Out.WriteLine($"Heroes rebuilt: {result.HeroesRebuilt}");
        Console.Out.WriteLine($"Hero skills rebuilt: {result.HeroSkillsRebuilt}");
        Console.Out.WriteLine($"Hero project stats rebuilt: {result.HeroProjectStatsRebuilt}");
        Console.Out.WriteLine($"Healthy: {result.After.Healthy}");
        return result.After.Healthy ? 0 : 1;
    }

    private static async Task<int> RunBackupAsync(
        string outputPath,
        bool json,
        CancellationToken cancellationToken)
    {
        var databasePath = HeroPassportRuntimePaths.ResolveDatabasePath();
        var result = await HeroPassportDatabaseBackup
            .CreateAsync(databasePath, outputPath, cancellationToken)
            .ConfigureAwait(false);

        if (json)
        {
            Console.Out.WriteLine(JsonSerializer.Serialize(result, CliJsonOptions));
            return result.Validated ? 0 : 1;
        }

        Console.Out.WriteLine($"Backup validated: {result.Validated}");
        Console.Out.WriteLine($"Destination: {result.DestinationPath}");
        Console.Out.WriteLine($"Size bytes: {result.SizeBytes.ToString(CultureInfo.InvariantCulture)}");
        Console.Out.WriteLine($"Migration state: {result.MigrationState}");
        return result.Validated ? 0 : 1;
    }

    private static async Task<int> RunExportAsync(
        string outputPath,
        bool json,
        CancellationToken cancellationToken)
    {
        var databasePath = HeroPassportRuntimePaths.ResolveDatabasePath();
        var result = await HeroPassportDataExport
            .CreateAsync(databasePath, outputPath, cancellationToken)
            .ConfigureAwait(false);

        if (json)
        {
            Console.Out.WriteLine(JsonSerializer.Serialize(result, CliJsonOptions));
            return 0;
        }

        Console.Out.WriteLine($"Export schema: {result.SchemaVersion}");
        Console.Out.WriteLine($"Destination: {result.DestinationPath}");
        Console.Out.WriteLine($"Size bytes: {result.SizeBytes.ToString(CultureInfo.InvariantCulture)}");
        return 0;
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

    private static async Task<int> RunDeleteHeroAsync(
        string heroIdValue,
        bool confirmed,
        bool json,
        CancellationToken cancellationToken)
    {
        if (!confirmed)
        {
            throw new HeroPassportException("HP300", "Option --confirm-logical-delete is required.");
        }

        var heroId = ParseHeroId(heroIdValue);
        var databasePath = HeroPassportRuntimePaths.ResolveDatabasePath();
        await HeroPassportDatabase.InitializeAsync(databasePath, cancellationToken).ConfigureAwait(false);
        var application = new HeroPassportApplication(new SqliteHeroPassportStateStore(databasePath), TimeProvider.System);
        await application.DeleteHeroPermanentlyAsync(heroId, cancellationToken).ConfigureAwait(false);

        if (json)
        {
            var payload = new
            {
                heroId = heroId.ToString(),
                deleted = true,
                deletionScope = "logical_application_state",
                forensicErasure = false,
            };
            Console.Out.WriteLine(JsonSerializer.Serialize(payload, CliJsonOptions));
            return 0;
        }

        Console.Out.WriteLine($"Hero {heroId} permanently deleted from Hero Passport application state.");
        Console.Out.WriteLine("This is logical deletion and does not provide forensic erasure of storage media.");
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

    private static HeroId ParseHeroId(string value)
    {
        try
        {
            return HeroId.Parse(value);
        }
        catch (FormatException)
        {
            throw new HeroPassportException("HP300", "Invalid heroId.");
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
