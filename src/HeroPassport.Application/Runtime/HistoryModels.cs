using HeroPassport.Domain.Primitives;

namespace HeroPassport.Application.Runtime;

public sealed record ProjectQuestHistoryResult(
    string ProjectDisplayName,
    IReadOnlyList<ProjectQuestHistoryItem> Items);

public sealed record ProjectQuestHistoryItem(
    QuestId QuestId,
    string HeroName,
    string QuestType,
    string Title,
    string Status,
    string? Result,
    long? XpGained,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? FinishedAtUtc);

public sealed record QuestHistoryDetailResult(
    QuestId QuestId,
    string HeroName,
    string ProjectDisplayName,
    string QuestType,
    string Title,
    string Goal,
    string Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? FinishedAtUtc,
    QuestHistoryReport? Report);

public sealed record QuestHistoryReport(
    string Result,
    string Summary,
    bool TestsMentioned,
    int ScopeViolations,
    int UserCorrections,
    string BuildStatus,
    string BuildEvidence,
    string TestsStatus,
    string TestsEvidence,
    long XpGained,
    IReadOnlyList<string> SkillsUsed);
