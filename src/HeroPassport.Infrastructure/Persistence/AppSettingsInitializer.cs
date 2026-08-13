using Microsoft.Data.Sqlite;

namespace HeroPassport.Infrastructure.Persistence;

internal static class AppSettingsInitializer
{
    public static async Task EnsureCreatedAsync(
        string databasePath,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await HeroPassportDatabase.OpenConnectionAsync(databasePath, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT OR IGNORE INTO app_settings(
                id, setup_completed, active_hero_id, locale, presentation_style,
                auto_start_quest, auto_finish_quest, project_identity_salt_v1,
                config_version, created_at_utc, updated_at_utc)
            VALUES(
                1, 0, NULL, 'en-US', 'rpg_engineering',
                1, 1, randomblob(32),
                1, strftime('%Y-%m-%dT%H:%M:%fZ','now'), strftime('%Y-%m-%dT%H:%M:%fZ','now'));
            """;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
