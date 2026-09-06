namespace HeroPassport.Infrastructure.Persistence;

internal enum PersistenceCommitPhase
{
    BeforeCommit,
    AfterCommit,
}

internal interface IPersistenceCommitObserver
{
    void OnCommitBoundary(string operation, PersistenceCommitPhase phase);
}
