using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;

namespace HeroPassport.Web.Services;

public sealed class StartQuestForm
{
    public string? QuestType { get; set; }
    public string? Title { get; set; }
    public string? Goal { get; set; }
}

public sealed record StartQuestPageViewModel(
    bool Available,
    string ProjectDisplayName,
    string? HeroName,
    bool HasOpenQuest,
    string? Message);

public enum PrepareStartQuestWebStatus
{
    Prepared,
    Unavailable,
    ValidationError,
    Conflict,
    Capacity,
}

public sealed record PrepareStartQuestWebResult(
    PrepareStartQuestWebStatus Status,
    string? Handle = null,
    string? Message = null);

public enum ConfirmStartQuestWebStatus
{
    Ready,
    Invalid,
    Gone,
    Busy,
    Committed,
}

public sealed record StartQuestConfirmationViewModel(
    string HeroName,
    string ProjectDisplayName,
    string QuestType,
    string Title,
    string Goal);

public sealed record ConfirmStartQuestWebResult(
    ConfirmStartQuestWebStatus Status,
    StartQuestConfirmationViewModel? Confirmation = null);

public enum CommitStartQuestWebStatus
{
    Success,
    Invalid,
    Gone,
    Busy,
    Conflict,
}

public sealed record CommitStartQuestWebResult(
    CommitStartQuestWebStatus Status,
    string? Message = null);

internal sealed class HeroPassportStartQuestService(
    HeroPassportApplication application,
    ProjectBindingContext project,
    PendingStartQuestStore pending)
{
    private const string UnavailableMessage = "Start Quest is unavailable until Hero Passport setup is complete.";
    private const string OpenQuestMessage = "This Hero already has an open Quest in the current project.";
    private const string ValidationMessage = "Quest type, title, or goal is invalid.";
    private const string CapacityMessage = "Too many pending confirmations. Try again.";
    private const string ConflictMessage = "The Quest could not be started because the current Hero or project state changed.";

    public async Task<StartQuestPageViewModel> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        var context = await application
            .GetRuntimeContextAsync(project, cancellationToken)
            .ConfigureAwait(false);

        if (!context.SetupCompleted || context.ActiveHero is null)
        {
            return new(false, context.Project.DisplayName, null, false, UnavailableMessage);
        }

        var hasOpenQuest = context.OpenQuests.Any(
            quest => quest.HeroId == context.ActiveHero.HeroId);
        return new(
            !hasOpenQuest,
            context.Project.DisplayName,
            context.ActiveHero.Name,
            hasOpenQuest,
            hasOpenQuest ? OpenQuestMessage : null);
    }

    public async Task<PrepareStartQuestWebResult> PrepareAsync(
        StartQuestForm input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var context = await application
            .GetRuntimeContextAsync(project, cancellationToken)
            .ConfigureAwait(false);

        if (!context.SetupCompleted || context.ActiveHero is null)
        {
            return new(PrepareStartQuestWebStatus.Unavailable, Message: UnavailableMessage);
        }

        if (context.OpenQuests.Any(quest => quest.HeroId == context.ActiveHero.HeroId))
        {
            return new(PrepareStartQuestWebStatus.Conflict, Message: OpenQuestMessage);
        }

        PreparedStartQuest prepared;
        try
        {
            prepared = HeroPassportApplication.PrepareStartQuest(
                new StartQuestRequest(
                    MutationRequestId.New(),
                    context.ActiveHero.HeroId,
                    input.QuestType ?? string.Empty,
                    input.Title ?? string.Empty,
                    input.Goal ?? string.Empty),
                project);
        }
        catch (HeroPassportException exception) when (
            exception.Code is "HP100" or "HP110" or "HP310")
        {
            return new(PrepareStartQuestWebStatus.ValidationError, Message: ValidationMessage);
        }

        if (!pending.TryInsert(
                prepared,
                context.ActiveHero.Name,
                context.Project.DisplayName,
                out var handle))
        {
            return new(PrepareStartQuestWebStatus.Capacity, Message: CapacityMessage);
        }

        return new(PrepareStartQuestWebStatus.Prepared, handle);
    }

    public ConfirmStartQuestWebResult LoadConfirmation(string? handle)
    {
        var lookup = pending.Lookup(handle);
        return lookup.Status switch
        {
            PendingStartQuestAccessStatus.Found when lookup.Entry is not null =>
                new(
                    ConfirmStartQuestWebStatus.Ready,
                    new StartQuestConfirmationViewModel(
                        lookup.Entry.HeroName,
                        lookup.Entry.ProjectDisplayName,
                        lookup.Entry.Prepared.QuestType,
                        lookup.Entry.Prepared.Title,
                        lookup.Entry.Prepared.Goal)),
            PendingStartQuestAccessStatus.Invalid => new(ConfirmStartQuestWebStatus.Invalid),
            PendingStartQuestAccessStatus.Gone => new(ConfirmStartQuestWebStatus.Gone),
            PendingStartQuestAccessStatus.Busy => new(ConfirmStartQuestWebStatus.Busy),
            PendingStartQuestAccessStatus.Committed => new(ConfirmStartQuestWebStatus.Committed),
            _ => new(ConfirmStartQuestWebStatus.Gone),
        };
    }

    public async Task<CommitStartQuestWebResult> CommitAsync(
        string? handle,
        CancellationToken cancellationToken = default)
    {
        var claim = pending.TryClaim(handle);
        if (claim.Status == PendingStartQuestAccessStatus.Invalid)
        {
            return new(CommitStartQuestWebStatus.Invalid);
        }

        if (claim.Status == PendingStartQuestAccessStatus.Gone)
        {
            return new(CommitStartQuestWebStatus.Gone);
        }

        if (claim.Status == PendingStartQuestAccessStatus.Busy)
        {
            return new(CommitStartQuestWebStatus.Busy);
        }

        if (claim.Status == PendingStartQuestAccessStatus.Committed)
        {
            return new(CommitStartQuestWebStatus.Success);
        }

        if (claim.Entry is null)
        {
            return new(CommitStartQuestWebStatus.Gone);
        }

        var prepared = claim.Entry.Prepared;
        try
        {
            await application.StartQuestAsync(
                    new StartQuestRequest(
                        prepared.StartRequestId,
                        prepared.HeroId,
                        prepared.QuestType,
                        prepared.Title,
                        prepared.Goal),
                    project,
                    cancellationToken)
                .ConfigureAwait(false);
            pending.Complete(handle);
            return new(CommitStartQuestWebStatus.Success);
        }
        catch (HeroPassportException exception) when (
            exception.Code is "HP133" or "HP135" or "HP140" or "HP141")
        {
            pending.Release(handle);
            return new(CommitStartQuestWebStatus.Conflict, ConflictMessage);
        }
        catch
        {
            pending.Release(handle);
            throw;
        }
    }
}
