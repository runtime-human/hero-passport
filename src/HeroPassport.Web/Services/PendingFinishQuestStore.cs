using System.Security.Cryptography;
using HeroPassport.Application.Runtime;

namespace HeroPassport.Web.Services;

internal enum PendingFinishQuestAccessStatus
{
    Found,
    Invalid,
    Gone,
    Busy,
    Committed,
}

internal sealed record PendingFinishQuestEntry(
    PreparedFinishQuest Prepared,
    string HeroName,
    string ProjectDisplayName,
    string QuestType,
    string QuestTitle,
    string QuestGoal,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc);

internal sealed record PendingFinishQuestAccessResult(
    PendingFinishQuestAccessStatus Status,
    PendingFinishQuestEntry? Entry = null);

internal sealed class PendingFinishQuestStore(TimeProvider timeProvider)
{
    private const int Capacity = 8;
    private const int HandleBytes = 16;
    private const int HandleChars = 22;
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private readonly Lock _gate = new();
    private readonly Dictionary<string, StoredEntry> _entries = new(StringComparer.Ordinal);
    private long _sequence;

    public bool TryInsert(
        PreparedFinishQuest prepared,
        string heroName,
        string projectDisplayName,
        string questType,
        string questTitle,
        string questGoal,
        out string handle)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentNullException.ThrowIfNull(heroName);
        ArgumentNullException.ThrowIfNull(projectDisplayName);
        ArgumentNullException.ThrowIfNull(questType);
        ArgumentNullException.ThrowIfNull(questTitle);
        ArgumentNullException.ThrowIfNull(questGoal);

        lock (_gate)
        {
            var now = timeProvider.GetUtcNow();
            RemoveExpired(now);
            if (_entries.Count >= Capacity && !EvictOldestPending())
            {
                handle = string.Empty;
                return false;
            }

            do
            {
                handle = CreateHandle();
            }
            while (_entries.ContainsKey(handle));

            var entry = new PendingFinishQuestEntry(
                prepared,
                heroName,
                projectDisplayName,
                questType,
                questTitle,
                questGoal,
                now,
                now + Lifetime);
            _entries.Add(
                handle,
                new StoredEntry(entry, PendingFinishQuestState.Pending, _sequence++));
            return true;
        }
    }

    public PendingFinishQuestAccessResult Lookup(string? handle)
    {
        if (!IsValidHandle(handle))
        {
            return new(PendingFinishQuestAccessStatus.Invalid);
        }

        lock (_gate)
        {
            RemoveExpired(timeProvider.GetUtcNow());
            if (!_entries.TryGetValue(handle!, out var stored))
            {
                return new(PendingFinishQuestAccessStatus.Gone);
            }

            return stored.State switch
            {
                PendingFinishQuestState.Pending => new(PendingFinishQuestAccessStatus.Found, stored.Entry),
                PendingFinishQuestState.Committing => new(PendingFinishQuestAccessStatus.Busy),
                PendingFinishQuestState.Committed => new(PendingFinishQuestAccessStatus.Committed),
                _ => new(PendingFinishQuestAccessStatus.Gone),
            };
        }
    }

    public PendingFinishQuestAccessResult TryClaim(string? handle)
    {
        if (!IsValidHandle(handle))
        {
            return new(PendingFinishQuestAccessStatus.Invalid);
        }

        lock (_gate)
        {
            RemoveExpired(timeProvider.GetUtcNow());
            if (!_entries.TryGetValue(handle!, out var stored))
            {
                return new(PendingFinishQuestAccessStatus.Gone);
            }

            if (stored.State == PendingFinishQuestState.Committing)
            {
                return new(PendingFinishQuestAccessStatus.Busy);
            }

            if (stored.State == PendingFinishQuestState.Committed)
            {
                return new(PendingFinishQuestAccessStatus.Committed);
            }

            stored.State = PendingFinishQuestState.Committing;
            return new(PendingFinishQuestAccessStatus.Found, stored.Entry);
        }
    }

    public void Complete(string? handle) => Transition(handle, PendingFinishQuestState.Committing, PendingFinishQuestState.Committed);

    public void Release(string? handle) => Transition(handle, PendingFinishQuestState.Committing, PendingFinishQuestState.Pending);

    public void Remove(string? handle)
    {
        if (!IsValidHandle(handle))
        {
            return;
        }

        lock (_gate)
        {
            RemoveExpired(timeProvider.GetUtcNow());
            _entries.Remove(handle!);
        }
    }

    private void Transition(string? handle, PendingFinishQuestState expected, PendingFinishQuestState next)
    {
        if (!IsValidHandle(handle))
        {
            return;
        }

        lock (_gate)
        {
            RemoveExpired(timeProvider.GetUtcNow());
            if (_entries.TryGetValue(handle!, out var stored) && stored.State == expected)
            {
                stored.State = next;
            }
        }
    }

    private static string CreateHandle()
    {
        var bytes = RandomNumberGenerator.GetBytes(HandleBytes);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static bool IsValidHandle(string? handle)
    {
        if (handle is null || handle.Length != HandleChars)
        {
            return false;
        }

        foreach (var character in handle)
        {
            if (!(character is >= 'A' and <= 'Z'
                or >= 'a' and <= 'z'
                or >= '0' and <= '9'
                or '-'
                or '_'))
            {
                return false;
            }
        }

        return true;
    }

    private void RemoveExpired(DateTimeOffset now)
    {
        foreach (var handle in _entries
                     .Where(pair => pair.Value.Entry.ExpiresAtUtc <= now)
                     .Select(static pair => pair.Key)
                     .ToArray())
        {
            _entries.Remove(handle);
        }
    }

    private bool EvictOldestPending()
    {
        var oldest = _entries
            .Where(static pair => pair.Value.State == PendingFinishQuestState.Pending)
            .OrderBy(static pair => pair.Value.Sequence)
            .Select(static pair => pair.Key)
            .FirstOrDefault();
        return oldest is not null && _entries.Remove(oldest);
    }

    private enum PendingFinishQuestState
    {
        Pending,
        Committing,
        Committed,
    }

    private sealed class StoredEntry(
        PendingFinishQuestEntry entry,
        PendingFinishQuestState state,
        long sequence)
    {
        public PendingFinishQuestEntry Entry { get; } = entry;
        public PendingFinishQuestState State { get; set; } = state;
        public long Sequence { get; } = sequence;
    }
}
