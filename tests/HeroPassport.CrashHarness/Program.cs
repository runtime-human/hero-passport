using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using HeroPassport.Infrastructure.Persistence;

if (args.Length is not (5 or 7))
{
    return 2;
}

var operation = args[0];
var observerOperation = operation switch
{
    "bootstrap" => "bootstrap",
    "start" => "start_quest",
    "finish" => "finish_quest",
    _ => null,
};
if (observerOperation is null)
{
    return 2;
}

var databasePath = args[1];
var phase = args[2] switch
{
    "before-commit" => PersistenceCommitPhase.BeforeCommit,
    "after-commit" => PersistenceCommitPhase.AfterCommit,
    _ => throw new ArgumentException("Unknown crash phase."),
};
var requestId = MutationRequestId.Parse(args[3]);
var signalPath = Path.GetFullPath(args[4]);
var observer = new CommitBarrierObserver(observerOperation, phase, signalPath);
var application = new HeroPassportApplication(
    new SqliteHeroPassportStateStore(databasePath, observer),
    TimeProvider.System);

switch (operation)
{
    case "bootstrap" when args.Length == 5:
        await application.BootstrapAsync(
            new BootstrapRequest(requestId, "en-US", "Crash Nova", "rpg_engineering", true, true));
        break;

    case "start" when args.Length == 7:
        await application.StartQuestAsync(
            new StartQuestRequest(
                requestId,
                HeroId.Parse(args[5]),
                "coding",
                "Crash Start",
                "Qualify Start persistence recovery around COMMIT."),
            Project(args[6]));
        break;

    case "finish" when args.Length == 7:
        await application.FinishQuestAsync(
            new FinishQuestRequest(
                requestId,
                QuestId.Parse(args[5]),
                "success",
                "Qualify Finish persistence recovery around COMMIT.",
                new FinishQuestMetrics(false, 0, 0, "not_run", "none", "not_run", "none"),
                ["coding"]),
            Project(args[6]));
        break;

    default:
        return 2;
}

return 0;

static ProjectBindingContext Project(string fingerprint) =>
    new("Crash Project", fingerprint, "project-identity/1");

file sealed class CommitBarrierObserver(
    string expectedOperation,
    PersistenceCommitPhase expectedPhase,
    string signalPath) : IPersistenceCommitObserver
{
    public void OnCommitBoundary(string operation, PersistenceCommitPhase phase)
    {
        if (!string.Equals(operation, expectedOperation, StringComparison.Ordinal) || phase != expectedPhase)
        {
            return;
        }

        File.WriteAllText(signalPath, $"{operation}:{phase}");
        using var barrier = new ManualResetEventSlim(initialState: false);
        barrier.Wait();
    }
}
