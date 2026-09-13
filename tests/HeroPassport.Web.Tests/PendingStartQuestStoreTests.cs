using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using HeroPassport.Web.Services;
using Xunit;

namespace HeroPassport.Web.Tests;

public sealed class PendingStartQuestStoreTests
{
    [Fact]
    public void InsertAndLookupPreservePreparedCommandAndSafePresentation()
    {
        var clock = new ManualTimeProvider();
        var store = new PendingStartQuestStore(clock);
        var prepared = Prepared("First");

        Assert.True(store.TryInsert(prepared, "Ada", "Demo", out var handle));

        Assert.Matches("^[A-Za-z0-9_-]{22}$", handle);
        var lookup = store.Lookup(handle);
        Assert.Equal(PendingStartQuestAccessStatus.Found, lookup.Status);
        Assert.NotNull(lookup.Entry);
        Assert.Equal(prepared, lookup.Entry.Prepared);
        Assert.Equal("Ada", lookup.Entry.HeroName);
        Assert.Equal("Demo", lookup.Entry.ProjectDisplayName);
    }

    [Fact]
    public void NinthInsertEvictsOldestPendingEntry()
    {
        var clock = new ManualTimeProvider();
        var store = new PendingStartQuestStore(clock);
        var handles = new List<string>();
        for (var index = 0; index < 8; index++)
        {
            Assert.True(store.TryInsert(Prepared($"Quest {index}"), "Ada", "Demo", out var handle));
            handles.Add(handle);
            clock.Advance(TimeSpan.FromSeconds(1));
        }

        Assert.True(store.TryInsert(Prepared("Quest 8"), "Ada", "Demo", out var newest));

        Assert.Equal(PendingStartQuestAccessStatus.Gone, store.Lookup(handles[0]).Status);
        Assert.Equal(PendingStartQuestAccessStatus.Found, store.Lookup(handles[1]).Status);
        Assert.Equal(PendingStartQuestAccessStatus.Found, store.Lookup(newest).Status);
    }

    [Fact]
    public void ExpiredEntryIsGoneAfterTenMinutes()
    {
        var clock = new ManualTimeProvider();
        var store = new PendingStartQuestStore(clock);
        Assert.True(store.TryInsert(Prepared("First"), "Ada", "Demo", out var handle));

        clock.Advance(TimeSpan.FromMinutes(10) + TimeSpan.FromTicks(1));

        Assert.Equal(PendingStartQuestAccessStatus.Gone, store.Lookup(handle).Status);
    }

    [Fact]
    public void MalformedOrUnknownHandleFailsClosed()
    {
        var store = new PendingStartQuestStore(new ManualTimeProvider());

        Assert.Equal(PendingStartQuestAccessStatus.Invalid, store.Lookup("not a handle").Status);
        Assert.Equal(
            PendingStartQuestAccessStatus.Gone,
            store.Lookup("AAAAAAAAAAAAAAAAAAAAAA").Status);
    }

    [Fact]
    public void ClaimIsAtomicAndCommittedEntryReplaysWithoutAnotherClaim()
    {
        var store = new PendingStartQuestStore(new ManualTimeProvider());
        Assert.True(store.TryInsert(Prepared("First"), "Ada", "Demo", out var handle));

        var first = store.TryClaim(handle);
        var concurrent = store.TryClaim(handle);
        store.Complete(handle);
        var committed = store.TryClaim(handle);

        Assert.Equal(PendingStartQuestAccessStatus.Found, first.Status);
        Assert.NotNull(first.Entry);
        Assert.Equal(PendingStartQuestAccessStatus.Busy, concurrent.Status);
        Assert.Equal(PendingStartQuestAccessStatus.Committed, committed.Status);
    }

    [Fact]
    public void RetryableFailureReleasesSamePreparedRequestId()
    {
        var store = new PendingStartQuestStore(new ManualTimeProvider());
        var prepared = Prepared("First");
        Assert.True(store.TryInsert(prepared, "Ada", "Demo", out var handle));
        Assert.Equal(PendingStartQuestAccessStatus.Found, store.TryClaim(handle).Status);

        store.Release(handle);
        var retried = store.TryClaim(handle);

        Assert.Equal(PendingStartQuestAccessStatus.Found, retried.Status);
        Assert.NotNull(retried.Entry);
        Assert.Equal(prepared.StartRequestId, retried.Entry.Prepared.StartRequestId);
    }

    [Fact]
    public void InsertFailsWhenCapacityContainsOnlyCommittingEntries()
    {
        var store = new PendingStartQuestStore(new ManualTimeProvider());
        for (var index = 0; index < 8; index++)
        {
            Assert.True(store.TryInsert(Prepared($"Quest {index}"), "Ada", "Demo", out var handle));
            Assert.Equal(PendingStartQuestAccessStatus.Found, store.TryClaim(handle).Status);
        }

        Assert.False(store.TryInsert(Prepared("Overflow"), "Ada", "Demo", out var rejected));
        Assert.Equal(string.Empty, rejected);
    }

    private static PreparedStartQuest Prepared(string title) =>
        new(
            MutationRequestId.New(),
            HeroId.New(),
            "coding",
            title,
            "Ship it");

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = new(2026, 9, 13, 9, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan amount) => _utcNow += amount;
    }
}
