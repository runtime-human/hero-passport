using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using HeroPassport.Infrastructure.Persistence;
using System.CommandLine;
using System.Text.Json;

namespace HeroPassport.App;

internal static class HeroPassportHeroCli
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Command CreateCommand()
    {
        var heroCommand = new Command("hero", "Manage local Hero profiles.");
        heroCommand.Subcommands.Add(CreateCreateCommand());
        heroCommand.Subcommands.Add(CreateListCommand());
        heroCommand.Subcommands.Add(CreatePreferenceCommand(
            "activate",
            "Make a non-archived Hero the default for future Quests.",
            static (application, heroId, token) => application.ActivateHeroPreferenceAsync(heroId, token)));
        heroCommand.Subcommands.Add(CreatePreferenceCommand(
            "archive",
            "Archive an inactive Hero with no open Quests.",
            static (application, heroId, token) => application.ArchiveHeroAsync(heroId, token)));
        heroCommand.Subcommands.Add(CreatePreferenceCommand(
            "restore",
            "Restore an archived Hero without activating it.",
            static (application, heroId, token) => application.RestoreHeroAsync(heroId, token)));
        return heroCommand;
    }

    private static Command CreateCreateCommand()
    {
        var nameOption = new Option<string>("--name")
        {
            Description = "Hero name.",
            Required = true,
        };
        var requestIdOption = new Option<string?>("--request-id")
        {
            Description = "Optional lowercase canonical UUIDv7 create request identity for retry-safe scripts.",
        };
        var jsonOption = JsonOption();
        var command = new Command("create", "Create a Hero without changing the active default.")
        {
            nameOption,
            requestIdOption,
            jsonOption,
        };
        command.SetAction((parseResult, token) => RunCreateAsync(
            parseResult.GetValue(nameOption)!,
            parseResult.GetValue(requestIdOption),
            parseResult.GetValue(jsonOption),
            token));
        return command;
    }

    private static Command CreateListCommand()
    {
        var jsonOption = JsonOption();
        var command = new Command("list", "List Heroes in deterministic administration order.")
        {
            jsonOption,
        };
        command.SetAction((parseResult, token) => RunListAsync(parseResult.GetValue(jsonOption), token));
        return command;
    }

    private static Command CreatePreferenceCommand(
        string name,
        string description,
        Func<HeroPassportApplication, HeroId, CancellationToken, Task<HeroPreferenceChangeResult>> action)
    {
        var heroIdOption = new Option<string>("--hero-id")
        {
            Description = "Lowercase canonical UUIDv7 Hero identity.",
            Required = true,
        };
        var jsonOption = JsonOption();
        var command = new Command(name, description)
        {
            heroIdOption,
            jsonOption,
        };
        command.SetAction((parseResult, token) => RunPreferenceChangeAsync(
            name,
            parseResult.GetValue(heroIdOption)!,
            parseResult.GetValue(jsonOption),
            action,
            token));
        return command;
    }

    private static Option<bool> JsonOption() => new("--json")
    {
        Description = "Write one machine-readable JSON result to stdout.",
    };

    private static async Task<int> RunCreateAsync(
        string name,
        string? requestId,
        bool json,
        CancellationToken cancellationToken)
    {
        var createRequestId = ParseOrCreateRequestId(requestId);
        var application = await CreateApplicationAsync(cancellationToken).ConfigureAwait(false);
        var result = await application
            .CreateHeroAsync(new CreateHeroRequest(createRequestId, name), cancellationToken)
            .ConfigureAwait(false);

        if (json)
        {
            WriteJson(new
            {
                createRequestId = createRequestId.ToString(),
                hero = new
                {
                    heroId = result.Hero.HeroId.ToString(),
                    name = result.Hero.Name,
                },
                replayed = result.Replayed,
            });
            return 0;
        }

        Console.Out.WriteLine(result.Replayed ? "Hero creation replayed." : "Hero created.");
        Console.Out.WriteLine($"Hero: {result.Hero.Name}");
        Console.Out.WriteLine($"Hero ID: {result.Hero.HeroId}");
        Console.Out.WriteLine($"Create request ID: {createRequestId}");
        return 0;
    }

    private static async Task<int> RunListAsync(bool json, CancellationToken cancellationToken)
    {
        var application = await CreateApplicationAsync(cancellationToken).ConfigureAwait(false);
        var result = await application.ListHeroesAsync(cancellationToken).ConfigureAwait(false);
        var heroes = result.Heroes.Select(ToCliSnapshot).ToArray();

        if (json)
        {
            WriteJson(new { heroes });
            return 0;
        }

        foreach (var hero in heroes)
        {
            var state = hero.Active ? "active" : hero.Archived ? "archived" : "available";
            Console.Out.WriteLine($"{hero.Name} | {hero.HeroId} | L{hero.Level} {hero.RankKey} | {state}");
        }

        return 0;
    }

    private static async Task<int> RunPreferenceChangeAsync(
        string operation,
        string heroId,
        bool json,
        Func<HeroPassportApplication, HeroId, CancellationToken, Task<HeroPreferenceChangeResult>> action,
        CancellationToken cancellationToken)
    {
        var parsedHeroId = ParseHeroId(heroId);
        var application = await CreateApplicationAsync(cancellationToken).ConfigureAwait(false);
        var result = await action(application, parsedHeroId, cancellationToken).ConfigureAwait(false);
        var hero = ToCliSnapshot(result.Hero);

        if (json)
        {
            WriteJson(new { hero, changed = result.Changed });
            return 0;
        }

        Console.Out.WriteLine(result.Changed
            ? $"Hero {operation} completed."
            : $"Hero {operation} already satisfied.");
        Console.Out.WriteLine($"Hero: {hero.Name}");
        Console.Out.WriteLine($"Hero ID: {hero.HeroId}");
        return 0;
    }

    private static async Task<HeroPassportApplication> CreateApplicationAsync(CancellationToken cancellationToken)
    {
        var databasePath = HeroPassportRuntimePaths.ResolveDatabasePath();
        await HeroPassportDatabase.InitializeAsync(databasePath, cancellationToken).ConfigureAwait(false);
        return new HeroPassportApplication(new SqliteHeroPassportStateStore(databasePath), TimeProvider.System);
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

    private static HeroCliSnapshot ToCliSnapshot(HeroListItemSnapshot hero) => new(
        hero.HeroId.ToString(),
        hero.Name,
        hero.Archived,
        hero.Active,
        hero.TotalXp,
        hero.Level,
        hero.RankKey,
        hero.Trust,
        hero.Strain);

    private static void WriteJson<T>(T value) =>
        Console.Out.WriteLine(JsonSerializer.Serialize(value, JsonOptions));

    private sealed record HeroCliSnapshot(
        string HeroId,
        string Name,
        bool Archived,
        bool Active,
        long TotalXp,
        int Level,
        string RankKey,
        int Trust,
        int Strain);
}
