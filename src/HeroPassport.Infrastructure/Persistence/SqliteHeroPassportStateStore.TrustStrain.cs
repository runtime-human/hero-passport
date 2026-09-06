using HeroPassport.Application.Runtime;
using HeroPassport.Domain.Engine;
using HeroPassport.Domain.Primitives;
using Microsoft.Data.Sqlite;

namespace HeroPassport.Infrastructure.Persistence;

public sealed partial class SqliteHeroPassportStateStore
{
    private static async Task InsertTrustStrainComponentsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        QuestReportId reportId,
        IReadOnlyList<TrustStrainComponent> components,
        CancellationToken cancellationToken)
    {
        for (var ordinal = 0; ordinal < components.Count; ordinal++)
        {
            var component = components[ordinal];
            await ExecuteAsync(
                connection,
                transaction,
                "INSERT INTO quest_trust_strain_components(quest_report_id,ordinal,component_key,trust_delta,strain_delta) VALUES($report,$ordinal,$key,$trust,$strain);",
                cancellationToken,
                ("$report", reportId.ToString()),
                ("$ordinal", ordinal),
                ("$key", component.Key),
                ("$trust", component.TrustDelta),
                ("$strain", component.StrainDelta)).ConfigureAwait(false);
        }
    }

    private static async Task<IReadOnlyList<TrustStrainComponentSnapshot>> TrustStrainComponentsForReportAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        QuestReportId reportId,
        CancellationToken cancellationToken)
    {
        await using var command = Command(
            connection,
            transaction,
            """
            SELECT component_key,trust_delta,strain_delta
            FROM quest_trust_strain_components
            WHERE quest_report_id=$report
            ORDER BY ordinal;
            """,
            ("$report", reportId.ToString()));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var snapshots = new List<TrustStrainComponentSnapshot>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            snapshots.Add(new TrustStrainComponentSnapshot(
                reader.GetString(0),
                reader.GetInt32(1),
                reader.GetInt32(2)));
        }

        return snapshots;
    }

    private static TrustStrainSnapshot TrustStrainSnapshot(TrustStrainResult result) =>
        new(
            result.TrustBefore,
            result.TrustAfter,
            result.StrainBefore,
            result.StrainAfter,
            result.Components
                .Select(static component => new TrustStrainComponentSnapshot(
                    component.Key,
                    component.TrustDelta,
                    component.StrainDelta))
                .ToArray(),
            result.RuleVersion);
}
