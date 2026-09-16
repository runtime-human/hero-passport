using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;

namespace HeroPassport.Web.Services;

public sealed class FinishQuestForm
{
    public string? Result { get; set; } = "success";
    public string? Summary { get; set; }
    public bool TestsMentioned { get; set; }
    public int ScopeViolations { get; set; }
    public int UserCorrections { get; set; }
    public string? BuildStatus { get; set; } = "not_run";
    public string? BuildEvidence { get; set; } = "none";
    public string? TestsStatus { get; set; } = "not_run";
    public string? TestsEvidence { get; set; } = "none";
    public string[] SkillsUsed { get; set; } = [];
}

public sealed class ConfirmFinishQuestForm;

public enum FinishQuestPageStatus
{
    Ready,
    Invalid,
    NotFound,
}

public sealed record FinishQuestPageViewModel(
    string HeroName,
    string ProjectDisplayName,
    string QuestType,
    string QuestTitle,
    string QuestGoal);

public sealed record LoadFinishQuestWebResult(
    FinishQuestPageStatus Status,
    FinishQuestPageViewModel? Page = null);

public enum PrepareFinishQuestWebStatus
{
    Prepared,
    Invalid,
    NotFound,
    ValidationError,
    Capacity,
}

public sealed record PrepareFinishQuestWebResult(
    PrepareFinishQuestWebStatus Status,
    string? Handle = null,
    string? Message = null);

public enum ConfirmFinishQuestWebStatus
{
    Ready,
    Invalid,
    Gone,
    Busy,
    Committed,
}

public sealed record FinishQuestConfirmationViewModel(
    string HeroName,
    string ProjectDisplayName,
    string QuestType,
    string QuestTitle,
    string QuestGoal,
    string Result,
    string Summary,
    FinishQuestMetrics Metrics,
    IReadOnlyList<string> SkillsUsed);

public sealed record ConfirmFinishQuestWebResult(
    ConfirmFinishQuestWebStatus Status,
    FinishQuestConfirmationViewModel? Confirmation = null);

public enum CommitFinishQuestWebStatus
{
    Success,
    Invalid,
    Gone,
    Busy,
    Conflict,
}

public sealed record CommitFinishQuestWebResult(
    CommitFinishQuestWebStatus Status,
    string? Message = null);

internal sealed class HeroPassportFinishQuestService(
    HeroPassportApplication application,
    ProjectBindingContext project,
    PendingFinishQuestStore pending)
{
    private const string ValidationMessage = "Finish result, summary, attestations, or Skills are invalid.";
    private const string CapacityMessage = "Too many pending confirmations. Try again.";
    private const string ConflictMessage = "The Quest could not be finished because its durable state changed.";

    public async Task<LoadFinishQuestWebResult> LoadAsync(
        string? questId,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseQuestId(questId, out var parsedQuestId))
        {
            return new(FinishQuestPageStatus.Invalid);
        }

        var context = await application
            .GetRuntimeContextAsync(project, cancellationToken)
            .ConfigureAwait(false);
        var quest = FindOpenQuest(context, parsedQuestId);
        if (quest is null)
        {
            return new(FinishQuestPageStatus.NotFound);
        }

        return new(
            FinishQuestPageStatus.Ready,
            new FinishQuestPageViewModel(
                quest.HeroName,
                context.Project.DisplayName,
                quest.QuestType,
                quest.Title,
                quest.Goal));
    }

    public async Task<PrepareFinishQuestWebResult> PrepareAsync(
        string? questId,
        FinishQuestForm input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!TryParseQuestId(questId, out var parsedQuestId))
        {
            return new(PrepareFinishQuestWebStatus.Invalid);
        }

        var context = await application
            .GetRuntimeContextAsync(project, cancellationToken)
            .ConfigureAwait(false);
        var quest = FindOpenQuest(context, parsedQuestId);
        if (quest is null)
        {
            return new(PrepareFinishQuestWebStatus.NotFound);
        }

        PreparedFinishQuest prepared;
        try
        {
            prepared = HeroPassportApplication.PrepareFinishQuest(
                new FinishQuestRequest(
                    MutationRequestId.New(),
                    parsedQuestId,
                    input.Result ?? string.Empty,
                    input.Summary ?? string.Empty,
                    new FinishQuestMetrics(
                        input.TestsMentioned,
                        input.ScopeViolations,
                        input.UserCorrections,
                        input.BuildStatus ?? string.Empty,
                        input.BuildEvidence ?? string.Empty,
                        input.TestsStatus ?? string.Empty,
                        input.TestsEvidence ?? string.Empty),
                    input.SkillsUsed ?? []),
                project);
        }
        catch (HeroPassportException exception) when (
            exception.Code is "HP100" or "HP111" or "HP112" or "HP120" or "HP310")
        {
            return new(PrepareFinishQuestWebStatus.ValidationError, Message: ValidationMessage);
        }

        if (!pending.TryInsert(
                prepared,
                quest.HeroName,
                context.Project.DisplayName,
                quest.QuestType,
                quest.Title,
                quest.Goal,
                out var handle))
        {
            return new(PrepareFinishQuestWebStatus.Capacity, Message: CapacityMessage);
        }

        return new(PrepareFinishQuestWebStatus.Prepared, handle);
    }

    public ConfirmFinishQuestWebResult LoadConfirmation(string? handle)
    {
        var lookup = pending.Lookup(handle);
        return lookup.Status switch
        {
            PendingFinishQuestAccessStatus.Found when lookup.Entry is not null =>
                new(
                    ConfirmFinishQuestWebStatus.Ready,
                    ToConfirmation(lookup.Entry)),
            PendingFinishQuestAccessStatus.Invalid => new(ConfirmFinishQuestWebStatus.Invalid),
            PendingFinishQuestAccessStatus.Gone => new(ConfirmFinishQuestWebStatus.Gone),
            PendingFinishQuestAccessStatus.Busy => new(ConfirmFinishQuestWebStatus.Busy),
            PendingFinishQuestAccessStatus.Committed => new(ConfirmFinishQuestWebStatus.Committed),
            _ => new(ConfirmFinishQuestWebStatus.Gone),
        };
    }

    public async Task<CommitFinishQuestWebResult> CommitAsync(
        string? handle,
        CancellationToken cancellationToken = default)
    {
        var claim = pending.TryClaim(handle);
        if (claim.Status == PendingFinishQuestAccessStatus.Invalid)
        {
            return new(CommitFinishQuestWebStatus.Invalid);
        }

        if (claim.Status == PendingFinishQuestAccessStatus.Gone)
        {
            return new(CommitFinishQuestWebStatus.Gone);
        }

        if (claim.Status == PendingFinishQuestAccessStatus.Busy)
        {
            return new(CommitFinishQuestWebStatus.Busy);
        }

        if (claim.Status == PendingFinishQuestAccessStatus.Committed)
        {
            return new(CommitFinishQuestWebStatus.Success);
        }

        if (claim.Entry is null)
        {
            return new(CommitFinishQuestWebStatus.Gone);
        }

        var prepared = claim.Entry.Prepared;
        try
        {
            _ = await application.FinishQuestAsync(
                    new FinishQuestRequest(
                        prepared.FinishRequestId,
                        prepared.QuestId,
                        prepared.Result,
                        prepared.Summary,
                        prepared.Metrics,
                        prepared.SkillsUsed),
                    project,
                    cancellationToken)
                .ConfigureAwait(false);
            pending.Complete(handle);
            return new(CommitFinishQuestWebStatus.Success);
        }
        catch (HeroPassportException exception) when (
            exception.Code is "HP001" or "HP130" or "HP134" or "HP135" or "HP136" or "HP140")
        {
            pending.Remove(handle);
            return new(CommitFinishQuestWebStatus.Conflict, ConflictMessage);
        }
        catch
        {
            pending.Release(handle);
            throw;
        }
    }

    private static FinishQuestConfirmationViewModel ToConfirmation(PendingFinishQuestEntry entry) =>
        new(
            entry.HeroName,
            entry.ProjectDisplayName,
            entry.QuestType,
            entry.QuestTitle,
            entry.QuestGoal,
            entry.Prepared.Result,
            entry.Prepared.Summary,
            entry.Prepared.Metrics,
            entry.Prepared.SkillsUsed);

    private static OpenQuestContext? FindOpenQuest(RuntimeContextResult context, QuestId questId) =>
        context.OpenQuests.FirstOrDefault(quest => quest.QuestId == questId);

    private static bool TryParseQuestId(string? value, out QuestId questId)
    {
        try
        {
            questId = QuestId.Parse(value!);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentNullException or FormatException)
        {
            questId = default;
            return false;
        }
    }
}
