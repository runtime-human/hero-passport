using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using HeroPassport.Web.Services;
using Xunit;

namespace HeroPassport.Web.Tests;

public sealed class PendingFinishQuestStoreTests
{
    [Fact]
    public void InsertAndLookupPreservePreparedIntentAndSafeContext()
    {
        var clock = new ManualTimeProvider();
        var store = new PendingFinishQuestStore(clock);
        var prepared = Prepared("Done");

        Assert.True(store.TryInsert(
            prepared,
            "Ada",
            "Demo",
            "coding",
            "Build parser",
            "Ship bounded parser",
            out var handle));

        Assert.Matches("^[A-Za-z0-9_-]{22}$", handle);
        var lookup = store.Lookup(handle);
        Assert.Equal(PendingFinishQuestAccessStatus.Found, lookup.Status);
        Assert.NotNull(lookup.Entry);
        Assert.Equal(prepared, lookup.Entry.Prepared);
        Assert.Equal("Ada", lookup.Entry.HeroName);
        Assert.Equal("Demo", lookup.Entry.ProjectDisplayName);
        Assert.Equal("coding", lookup.Entry.QuestType);
        Assert.Equal("Build parser", lookup.Entry.QuestTitle);
        Assert.Equal("Ship bounded parser", lookup.Entry.QuestGoal);
    }

    [Fact]
    public void CapacityEvictsOnlyOldestPendingEntry()
    {
        var clock = new ManualTimeProvider();
        var store = new PendingFinishQuestStore(clock);
        var handles = new List<string>();
        for (var index = 0; index < 8; index++)
        {
            Assert.True(store.TryInsert(
                Prepared($"Done {index}"), "Ada", "Demo", "coding", $"Quest {index}", "Goal", out var handle));
            handles.Add(handle);
            clock.Advance(TimeSpan.FromSeconds(1));
        }

        Assert.True(store.TryInsert(Prepared("Overflow"), "Ada", "Demo", "coding", "Newest", "Goal", out var newest));
        Assert.Equal(PendingFinishQuestAccessStatus.Gone, store.Lookup(handles[0]).Status);
        Assert.Equal(PendingFinishQuestAccessStatus.Found, store.Lookup(handles[1]).Status);
        Assert.Equal(PendingFinishQuestAccessStatus.Found, store.Lookup(newest).Status);
    }

    [Fact]
    public void CommittingAndCommittedEntriesAreNeverCapacityEvictionCandidates()
    {
        var store = new PendingFinishQuestStore(new ManualTimeProvider());
        for (var index = 0; index < 8; index++)
        {
            Assert.True(store.TryInsert(
                Prepared($"Done {index}"), "Ada", "Demo", "coding", $"Quest {index}", "Goal", out var handle));
            Assert.Equal(PendingFinishQuestAccessStatus.Found, store.TryClaim(handle).Status);
            if (index % 2 == 0)
            {
                store.Complete(handle);
            }
        }

        Assert.False(store.TryInsert(Prepared("Overflow"), "Ada", "Demo", "coding", "Overflow", "Goal", out var rejected));
        Assert.Equal(string.Empty, rejected);
    }

    [Fact]
    public void ReleaseRetainsSameFinishRequestAndRemoveMakesHandleGone()
    {
        var store = new PendingFinishQuestStore(new ManualTimeProvider());
        var prepared = Prepared("Done");
        Assert.True(store.TryInsert(prepared, "Ada", "Demo", "coding", "Quest", "Goal", out var handle));
        Assert.Equal(PendingFinishQuestAccessStatus.Found, store.TryClaim(handle).Status);

        store.Release(handle);
        var retried = store.TryClaim(handle);
        Assert.Equal(PendingFinishQuestAccessStatus.Found, retried.Status);
        Assert.NotNull(retried.Entry);
        Assert.Equal(prepared.FinishRequestId, retried.Entry.Prepared.FinishRequestId);

        store.Remove(handle);
        Assert.Equal(PendingFinishQuestAccessStatus.Gone, store.Lookup(handle).Status);
    }

    [Fact]
    public void ExpiredMalformedAndUnknownHandlesFailClosed()
    {
        var clock = new ManualTimeProvider();
        var store = new PendingFinishQuestStore(clock);
        Assert.True(store.TryInsert(Prepared("Done"), "Ada", "Demo", "coding", "Quest", "Goal", out var handle));

        Assert.Equal(PendingFinishQuestAccessStatus.Invalid, store.Lookup("not a handle").Status);
        Assert.Equal(PendingFinishQuestAccessStatus.Gone, store.Lookup("AAAAAAAAAAAAAAAAAAAAAA").Status);

        clock.Advance(TimeSpan.FromMinutes(10) + TimeSpan.FromTicks(1));
        Assert.Equal(PendingFinishQuestAccessStatus.Gone, store.Lookup(handle).Status);
    }

    private static PreparedFinishQuest Prepared(string summary) =>
        new(
            MutationRequestId.New(),
            QuestId.New(),
            "success",
            summary,
            new FinishQuestMetrics(true, 0, 0, "passed", "observed", "passed", "observed"),
            ["coding", "testing_awareness"]);

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = new(2026, 9, 14, 8, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan amount) => _utcNow += amount;
    }
}
