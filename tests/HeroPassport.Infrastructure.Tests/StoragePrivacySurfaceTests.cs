using HeroPassport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HeroPassport.Infrastructure.Tests;

public sealed class StoragePrivacySurfaceTests
{
    private static readonly string[] ForbiddenStorageFragments =
    [
        "sourcecode",
        "filecontent",
        "diff",
        "patch",
        "rawlog",
        "prompt",
        "chattranscript",
        "secret",
        "apikey",
        "environmentdump",
        "workspacepath",
        "projectpath",
        "repositorypath",
        "remoteurl",
        "gitremote",
    ];

    [Fact]
    public void EfModelDoesNotPersistForbiddenRawEvidenceSecretsPathsOrRemoteUrls()
    {
        var options = new DbContextOptionsBuilder<HeroPassportDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        using var context = new HeroPassportDbContext(options);

        var propertyNames = context.Model
            .GetEntityTypes()
            .SelectMany(static entityType => entityType.GetProperties())
            .Select(static property => property.Name)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.Contains("title", propertyNames);
        Assert.Contains("goal", propertyNames);
        Assert.Contains("summary", propertyNames);
        Assert.Contains("workspace_fingerprint", propertyNames);

        var violations = propertyNames
            .Where(IsForbiddenStorageProperty)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    private static bool IsForbiddenStorageProperty(string propertyName)
    {
        var normalized = new string(propertyName
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());

        return ForbiddenStorageFragments.Any(normalized.Contains);
    }
}
