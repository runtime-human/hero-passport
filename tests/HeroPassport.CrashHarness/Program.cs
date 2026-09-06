using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using HeroPassport.Infrastructure.Persistence;

if (args.Length != 5 || !string.Equals(args[0], "bootstrap", StringComparison.Ordinal))
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

var observer = new CommitBarrierObserver("bootstrap", phase, signalPath);
var application = new HeroPassportApplication(
    new SqliteHeroPassportStateStore(databasePath, observer),
    TimeProvider.System);

await application.BootstrapAsync(
    new BootstrapRequest(requestId, "en-US", "Crash Nova", "rpg_engineering", true, true));

return 0;

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
