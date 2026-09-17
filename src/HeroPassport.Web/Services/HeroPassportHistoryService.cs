using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;

namespace HeroPassport.Web.Services;

public sealed class HeroPassportHistoryService
{
    private readonly HeroPassportApplication _application;
    private readonly ProjectBindingContext _project;

    public HeroPassportHistoryService(
        HeroPassportApplication application,
        ProjectBindingContext project)
    {
        _application = application;
        _project = project;
    }

    public async Task<LoadHistoryListWebResult> LoadListAsync(
        CancellationToken cancellationToken = default)
    {
        var context = await _application
            .GetRuntimeContextAsync(_project, cancellationToken)
            .ConfigureAwait(false);
        if (!context.SetupCompleted)
        {
            return new LoadHistoryListWebResult(QuestHistoryPageStatus.SetupRequired);
        }

        var history = await _application
            .GetProjectQuestHistoryAsync(_project, cancellationToken)
            .ConfigureAwait(false);
        var items = history.Items
            .Select(static item => new HeroPassportHistoryItemViewModel(
                item.QuestId.ToString(),
                item.HeroName,
                item.QuestType,
                item.Title,
                item.Status,
                item.Result,
                item.XpGained,
                item.StartedAtUtc,
                item.FinishedAtUtc))
            .ToArray();

        return new LoadHistoryListWebResult(
            QuestHistoryPageStatus.Ready,
            new HeroPassportHistoryListViewModel(history.ProjectDisplayName, items));
    }

    public async Task<LoadQuestHistoryWebResult> LoadDetailAsync(
        string routeQuestId,
        CancellationToken cancellationToken = default)
    {
        QuestId questId;
        try
        {
            questId = QuestId.Parse(routeQuestId);
        }
        catch (FormatException)
        {
            return new LoadQuestHistoryWebResult(QuestHistoryPageStatus.Invalid);
        }

        var context = await _application
            .GetRuntimeContextAsync(_project, cancellationToken)
            .ConfigureAwait(false);
        if (!context.SetupCompleted)
        {
            return new LoadQuestHistoryWebResult(QuestHistoryPageStatus.SetupRequired);
        }

        var detail = await _application
            .GetQuestHistoryDetailAsync(questId, _project, cancellationToken)
            .ConfigureAwait(false);
        if (detail is null)
        {
            return new LoadQuestHistoryWebResult(QuestHistoryPageStatus.NotFound);
        }

        HeroPassportQuestHistoryReportViewModel? report = null;
        if (detail.Report is { } sourceReport)
        {
            report = new HeroPassportQuestHistoryReportViewModel(
                sourceReport.Result,
                sourceReport.Summary,
                sourceReport.TestsMentioned,
                sourceReport.ScopeViolations,
                sourceReport.UserCorrections,
                sourceReport.BuildStatus,
                sourceReport.BuildEvidence,
                sourceReport.TestsStatus,
                sourceReport.TestsEvidence,
                sourceReport.XpGained,
                sourceReport.SkillsUsed.ToArray());
        }

        return new LoadQuestHistoryWebResult(
            QuestHistoryPageStatus.Ready,
            new HeroPassportQuestHistoryViewModel(
                detail.HeroName,
                detail.ProjectDisplayName,
                detail.QuestType,
                detail.Title,
                detail.Goal,
                detail.Status,
                detail.StartedAtUtc,
                detail.FinishedAtUtc,
                report));
    }
}

public enum QuestHistoryPageStatus
{
    Ready,
    SetupRequired,
    Invalid,
    NotFound,
}

public sealed record LoadHistoryListWebResult(
    QuestHistoryPageStatus Status,
    HeroPassportHistoryListViewModel? Page = null);

public sealed record LoadQuestHistoryWebResult(
    QuestHistoryPageStatus Status,
    HeroPassportQuestHistoryViewModel? Page = null);

public sealed record HeroPassportHistoryListViewModel(
    string ProjectDisplayName,
    IReadOnlyList<HeroPassportHistoryItemViewModel> Items);

public sealed record HeroPassportHistoryItemViewModel(
    string QuestId,
    string HeroName,
    string QuestType,
    string Title,
    string Status,
    string? Result,
    long? XpGained,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? FinishedAtUtc);

public sealed record HeroPassportQuestHistoryViewModel(
    string HeroName,
    string ProjectDisplayName,
    string QuestType,
    string Title,
    string Goal,
    string Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? FinishedAtUtc,
    HeroPassportQuestHistoryReportViewModel? Report);

public sealed record HeroPassportQuestHistoryReportViewModel(
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
