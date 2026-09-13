using System.Security.Cryptography;
using HeroPassport.Application.Runtime;

namespace HeroPassport.Web.Services;

internal enum PendingStartQuestAccessStatus
{
    Found,
    Invalid,
    Gone,
    Busy,
    Committed,
}

internal sealed record PendingStartQuestEntry(
    PreparedStartQuest Prepared,
    string HeroName,
    string ProjectDisplayName,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc);

internal sealed record PendingStartQuestAccessResult(
    PendingStartQuestAccessStatus Status,
    PendingStartQuestEntry? Entry = null);

internal sealed class PendingStartQuestStore(TimeProvider timeProvider)
{
    private const int Capacity = 8;
    private const int HandleBytes = 16;
    private const int HandleChars = 22;
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private readonly Lock _gate = new();
    private readonly Dictionary<string, StoredEntry> _entries = new(StringComparer.Ordinal);
    private long _sequence;

    public bool TryInsert(
        PreparedStartQuest prepared,
        string heroName,
        string projectDisplayName,
        out string handle)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentNullException.ThrowIfNull(heroName);
        ArgumentNullException.ThrowIfNull(projectDisplayName);

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

            var entry = new PendingStartQuestEntry(
                prepared,
                heroName,
                projectDisplayName,
                now,
                now + Lifetime);
            _entries.Add(
                handle,
                new StoredEntry(entry, PendingStartQuestState.Pending, _sequence++));
            return true;
        }
    }

    public PendingStartQuestAccessResult Lookup(string? handle)
    {
        if (!IsValidHandle(handle))
        {
            return new(PendingStartQuestAccessStatus.Invalid);
        }

        lock (_gate)
        {
            var now = timeProvider.GetUtcNow();
            RemoveExpired(now);
            if (!_entries.TryGetValue(handle!, out var stored))
            {
                return new(PendingStartQuestAccessStatus.Gone);
            }

            return stored.State switch
            {
                PendingStartQuestState.Pending => new(PendingStartQuestAccessStatus.Found, stored.Entry),
                PendingStartQuestState.Committing => new(PendingStartQuestAccessStatus.Busy),
                PendingStartQuestState.Committed => new(PendingStartQuestAccessStatus.Committed),
                _ => new(PendingStartQuestAccessStatus.Gone),
            };
        }
    }

    public PendingStartQuestAccessResult TryClaim(string? handle)
    {
        if (!IsValidHandle(handle))
        {
            return new(PendingStartQuestAccessStatus.Invalid);
        }

        lock (_gate)
        {
            var now = timeProvider.GetUtcNow();
            RemoveExpired(now);
            if (!_entries.TryGetValue(handle!, out var stored))
            {
                return new(PendingStartQuestAccessStatus.Gone);
            }

            if (stored.State == PendingStartQuestState.Committing)
            {
                return new(PendingStartQuestAccessStatus.Busy);
            }

            if (stored.State == PendingStartQuestState.Committed)
            {
                return new(PendingStartQuestAccessStatus.Committed);
            }

            stored.State = PendingStartQuestState.Committing;
            return new(PendingStartQuestAccessStatus.Found, stored.Entry);
        }
    }

    public void Complete(string? handle)
    {
        if (!IsValidHandle(handle))
        {
            return;
        }

        lock (_gate)
        {
            RemoveExpired(timeProvider.GetUtcNow());
            if (_entries.TryGetValue(handle!, out var stored)
                && stored.State == PendingStartQuestState.Committing)
            {
                stored.State = PendingStartQuestState.Committed;
            }
        }
    }

    public void Release(string? handle)
    {
        if (!IsValidHandle(handle))
        {
            return;
        }

        lock (_gate)
        {
            RemoveExpired(timeProvider.GetUtcNow());
            if (_entries.TryGetValue(handle!, out var stored)
                && stored.State == PendingStartQuestState.Committing)
            {
                stored.State = PendingStartQuestState.Pending;
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
            .Where(static pair => pair.Value.State == PendingStartQuestState.Pending)
            .OrderBy(static pair => pair.Value.Sequence)
            .Select(static pair => pair.Key)
            .FirstOrDefault();
        return oldest is not null && _entries.Remove(oldest);
    }

    private enum PendingStartQuestState
    {
        Pending,
        Committing,
        Committed,
    }

    private sealed class StoredEntry(
        PendingStartQuestEntry entry,
        PendingStartQuestState state,
        long sequence)
    {
        public PendingStartQuestEntry Entry { get; } = entry;
        public PendingStartQuestState State { get; set; } = state;
        public long Sequence { get; } = sequence;
    }
}
