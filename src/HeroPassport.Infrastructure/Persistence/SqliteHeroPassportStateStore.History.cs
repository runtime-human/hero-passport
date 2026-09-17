using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using Microsoft.Data.Sqlite;

namespace HeroPassport.Infrastructure.Persistence;

public sealed partial class SqliteHeroPassportStateStore
{
    public async Task<ProjectQuestHistoryResult> GetProjectQuestHistoryAsync(
        ProjectBindingContext project,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await HeroPassportDatabase
            .OpenConnectionAsync(_databasePath, cancellationToken)
            .ConfigureAwait(false);
        var existingProject = await FindHistoryProjectAsync(connection, project.WorkspaceFingerprint, cancellationToken)
            .ConfigureAwait(false);

        return new ProjectQuestHistoryResult(
            existingProject?.DisplayName ?? project.DisplayName,
            Array.Empty<ProjectQuestHistoryItem>());
    }

    public async Task<QuestHistoryDetailResult?> GetQuestHistoryDetailAsync(
        QuestId questId,
        ProjectBindingContext project,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await HeroPassportDatabase
            .OpenConnectionAsync(_databasePath, cancellationToken)
            .ConfigureAwait(false);
        var existingProject = await FindHistoryProjectAsync(connection, project.WorkspaceFingerprint, cancellationToken)
            .ConfigureAwait(false);

        return existingProject is null ? null : null;
    }

    private static async Task<HistoryProjectRow?> FindHistoryProjectAsync(
        SqliteConnection connection,
        string workspaceFingerprint,
        CancellationToken cancellationToken)
    {
        await using var command = Command(
            connection,
            null,
            "SELECT id,display_name FROM projects WHERE workspace_fingerprint=$fingerprint LIMIT 1;",
            ("$fingerprint", workspaceFingerprint));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new HistoryProjectRow(reader.GetString(0), reader.GetString(1));
    }

    private sealed record HistoryProjectRow(string Id, string DisplayName);
}
