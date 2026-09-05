using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using Microsoft.Data.Sqlite;
using System.Globalization;
using System.Security.Cryptography;

namespace HeroPassport.Infrastructure.Persistence;

public sealed partial class SqliteHeroPassportStateStore(string databasePath) : IHeroPassportStateStore
{
    private readonly string _databasePath = Path.GetFullPath(databasePath);

    private static SqliteCommand Command(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        }

        return command;
    }

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = Command(connection, transaction, sql, parameters);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<SettingsRow> SettingsAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        CancellationToken cancellationToken)
    {
        await using var command = Command(
            connection,
            transaction,
            "SELECT setup_completed,active_hero_id,locale,presentation_style,auto_start_quest,auto_finish_quest FROM app_settings WHERE id=1;");
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new HeroPassportException("HP900", "Application settings are unavailable.");
        }

        return new SettingsRow(
            reader.GetInt64(0) != 0,
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetInt64(4) != 0,
            reader.GetInt64(5) != 0);
    }

    private static async Task<ReceiptRow?> ReceiptAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string operation,
        string requestId,
        CancellationToken cancellationToken)
    {
        await using var command = Command(
            connection,
            transaction,
            "SELECT args_encoding_version,args_hash,result_entity_id FROM mutation_receipts WHERE operation_key=$operation AND request_id=$requestId;",
            ("$operation", operation),
            ("$requestId", requestId));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new ReceiptRow(reader.GetString(0), reader.GetFieldValue<byte[]>(1), reader.IsDBNull(2) ? null : reader.GetString(2));
    }

    private static void EnsureReceipt(ReceiptRow receipt, string encodingVersion, byte[] hash)
    {
        if (!string.Equals(receipt.EncodingVersion, encodingVersion, StringComparison.Ordinal) ||
            receipt.Hash.Length != hash.Length ||
            !CryptographicOperations.FixedTimeEquals(receipt.Hash, hash))
        {
            throw new HeroPassportException("HP135", "The mutation request ID was already used with different arguments.");
        }
    }

    private static async Task InsertReceiptAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string operation,
        string requestId,
        string encodingVersion,
        byte[] hash,
        string resultKind,
        string resultEntityId,
        string? heroId,
        string timestamp,
        CancellationToken cancellationToken)
    {
        await ExecuteAsync(
            connection,
            transaction,
            "INSERT INTO mutation_receipts(operation_key,request_id,args_encoding_version,args_hash,result_kind,result_entity_id,project_id,hero_id,result_status,effective_at_utc) VALUES($operation,$request,$encoding,$hash,$kind,$entity,NULL,$hero,'active',$time);",
            cancellationToken,
            ("$operation", operation),
            ("$request", requestId),
            ("$encoding", encodingVersion),
            ("$hash", hash),
            ("$kind", resultKind),
            ("$entity", resultEntityId),
            ("$hero", heroId),
            ("$time", timestamp)).ConfigureAwait(false);
    }

    private static async Task<HeroIdentitySnapshot> HeroAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string heroId,
        CancellationToken cancellationToken)
    {
        await using var command = Command(connection, transaction, "SELECT id,name FROM heroes WHERE id=$id AND archived_at_utc IS NULL;", ("$id", heroId));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new HeroPassportException("HP140", "Hero was not found.");
        }

        return new HeroIdentitySnapshot(HeroId.Parse(reader.GetString(0)), reader.GetString(1));
    }

    private static string Timestamp(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    private sealed record SettingsRow(
        bool SetupCompleted,
        string? ActiveHeroId,
        string Locale,
        string PresentationStyle,
        bool AutoStartQuest,
        bool AutoFinishQuest)
    {
        public SettingsSnapshot Snapshot() => new(Locale, PresentationStyle, AutoStartQuest, AutoFinishQuest);
    }

    private sealed record ReceiptRow(string EncodingVersion, byte[] Hash, string? ResultEntityId);
}
