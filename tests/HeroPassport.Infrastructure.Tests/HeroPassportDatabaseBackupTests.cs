using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Primitives;
using HeroPassport.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using System.Globalization;
using Xunit;

namespace HeroPassport.Infrastructure.Tests;

public sealed class HeroPassportDatabaseBackupTests
{
    [Fact]
    public async Task BackupPublishesValidatedSnapshotIncludingCommittedWalFrames()
    {
        var token = TestContext.Current.CancellationToken;
        var root = CreateRoot();
        var source = Path.Combine(root, "source", "hero-passport.db");
        var destination = Path.Combine(root, "backups", "hero-passport-backup.db");
        Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        try
        {
            await HeroPassportDatabase.InitializeAsync(source, token);
            var app = new HeroPassportApplication(new SqliteHeroPassportStateStore(source), TimeProvider.System);
            var hero = (await app.BootstrapAsync(
                new BootstrapRequest(MutationRequestId.New(), "en-US", "Nova", "rpg_engineering", true, true),
                token)).Hero;

            await using var pinnedReader = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = source,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false,
            }.ToString());
            await pinnedReader.OpenAsync(token);
            await using var pinnedTransaction = pinnedReader.BeginTransaction();
            await using (var pinCommand = pinnedReader.CreateCommand())
            {
                pinCommand.Transaction = pinnedTransaction;
                pinCommand.CommandText = "SELECT COUNT(*) FROM heroes;";
                Assert.Equal(
                    1L,
                    Convert.ToInt64(await pinCommand.ExecuteScalarAsync(token), CultureInfo.InvariantCulture));
            }

            var project = new ProjectBindingContext("Backup Qualification", new string('d', 64), "project-identity/1");
            var quest = (await app.StartQuestAsync(
                new StartQuestRequest(
                    MutationRequestId.New(), hero.HeroId, "coding",
                    "Qualify online backup",
                    "Commit data after a read snapshot is pinned so WAL frames must be included in the backup."),
                project,
                token)).Quest;
            await app.FinishQuestAsync(
                new FinishQuestRequest(
                    MutationRequestId.New(),
                    quest.QuestId,
                    "success",
                    "Completed online backup qualification with committed WAL-resident history and observed tests.",
                    new FinishQuestMetrics(true, 0, 0, "not_run", "none", "passed", "observed"),
                    ["coding"]),
                project,
                token);

            Assert.True(File.Exists(source + "-wal"), "Expected an active WAL while the old read snapshot is pinned.");

            var result = await HeroPassportDatabaseBackup.CreateAsync(source, destination, token);

            Assert.True(result.Validated);
            Assert.Equal(Path.GetFullPath(destination), result.DestinationPath);
            Assert.True(result.SizeBytes > 0);
            Assert.Equal("current", result.MigrationState);
            Assert.True(File.Exists(destination));

            await using var backup = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = destination,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false,
            }.ToString());
            await backup.OpenAsync(token);

            await using (var command = backup.CreateCommand())
            {
                command.CommandText = "SELECT total_xp,trust,strain,success_streak FROM heroes WHERE id=$id;";
                command.Parameters.AddWithValue("$id", hero.HeroId.ToString());
                await using var reader = await command.ExecuteReaderAsync(token);
                Assert.True(await reader.ReadAsync(token));
                Assert.Equal(95L, reader.GetInt64(0));
                Assert.Equal(52L, reader.GetInt64(1));
                Assert.Equal(18L, reader.GetInt64(2));
                Assert.Equal(1L, reader.GetInt64(3));
            }

            await using (var command = backup.CreateCommand())
            {
                command.CommandText = "SELECT COUNT(*) FROM quest_reports;";
                Assert.Equal(
                    1L,
                    Convert.ToInt64(await command.ExecuteScalarAsync(token), CultureInfo.InvariantCulture));
            }

            await pinnedTransaction.RollbackAsync(token);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task BackupNeverOverwritesAnExistingKnownGoodDestination()
    {
        var token = TestContext.Current.CancellationToken;
        var root = CreateRoot();
        var source = Path.Combine(root, "source.db");
        var destination = Path.Combine(root, "known-good.db");
        try
        {
            await HeroPassportDatabase.InitializeAsync(source, token);
            var knownGood = new byte[] { 0x48, 0x50, 0x2D, 0x42, 0x41, 0x43, 0x4B, 0x55, 0x50 };
            await File.WriteAllBytesAsync(destination, knownGood, token);

            await Assert.ThrowsAsync<IOException>(() =>
                HeroPassportDatabaseBackup.CreateAsync(source, destination, token));

            Assert.Equal(knownGood, await File.ReadAllBytesAsync(destination, token));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task MissingSourceNeverCreatesDestination()
    {
        var token = TestContext.Current.CancellationToken;
        var root = CreateRoot();
        var source = Path.Combine(root, "missing.db");
        var destination = Path.Combine(root, "backup.db");
        try
        {
            await Assert.ThrowsAsync<FileNotFoundException>(() =>
                HeroPassportDatabaseBackup.CreateAsync(source, destination, token));

            Assert.False(File.Exists(destination));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "HeroPassport.DatabaseBackup.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteRoot(string root)
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(root, recursive: true); } catch (DirectoryNotFoundException) { } catch (IOException) { }
    }
}
