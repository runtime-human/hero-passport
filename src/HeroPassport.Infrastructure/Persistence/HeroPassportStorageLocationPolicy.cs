namespace HeroPassport.Infrastructure.Persistence;

internal sealed record HeroPassportStorageLocationEvaluation(
    bool Supported,
    string Kind,
    string DriveType);

internal static class HeroPassportStorageLocationPolicy
{
    public static HeroPassportStorageLocationEvaluation Inspect(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        var fullPath = Path.GetFullPath(databasePath);
        if (OperatingSystem.IsWindows() && IsUncPath(fullPath))
        {
            return Classify(System.IO.DriveType.Network, isUnc: true);
        }

        var probePath = File.Exists(fullPath)
            ? fullPath
            : FindNearestExistingDirectory(fullPath);
        if (probePath is null)
        {
            return Classify(System.IO.DriveType.Unknown, isUnc: false);
        }

        try
        {
            var driveType = new DriveInfo(probePath).DriveType;
            return Classify(driveType, isUnc: false);
        }
        catch (ArgumentException)
        {
            return Classify(System.IO.DriveType.Unknown, isUnc: false);
        }
        catch (IOException)
        {
            return Classify(System.IO.DriveType.Unknown, isUnc: false);
        }
        catch (UnauthorizedAccessException)
        {
            return Classify(System.IO.DriveType.Unknown, isUnc: false);
        }
    }

    internal static HeroPassportStorageLocationEvaluation Classify(
        System.IO.DriveType driveType,
        bool isUnc)
    {
        if (isUnc)
        {
            return new HeroPassportStorageLocationEvaluation(
                Supported: false,
                Kind: "network",
                DriveType: "network");
        }

        var driveTypeName = driveType.ToString().ToLowerInvariant();
        return driveType switch
        {
            System.IO.DriveType.Fixed or
            System.IO.DriveType.Removable or
            System.IO.DriveType.Ram => new HeroPassportStorageLocationEvaluation(
                Supported: true,
                Kind: "local",
                DriveType: driveTypeName),
            System.IO.DriveType.Network => new HeroPassportStorageLocationEvaluation(
                Supported: false,
                Kind: "network",
                DriveType: driveTypeName),
            System.IO.DriveType.Unknown or
            System.IO.DriveType.NoRootDirectory => new HeroPassportStorageLocationEvaluation(
                Supported: false,
                Kind: "unknown",
                DriveType: driveTypeName),
            _ => new HeroPassportStorageLocationEvaluation(
                Supported: false,
                Kind: "unsupported",
                DriveType: driveTypeName),
        };
    }

    private static string? FindNearestExistingDirectory(string fullPath)
    {
        var candidate = Directory.Exists(fullPath)
            ? fullPath
            : Path.GetDirectoryName(fullPath);

        while (!string.IsNullOrWhiteSpace(candidate))
        {
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            var parent = Directory.GetParent(candidate);
            candidate = parent?.FullName;
        }

        return null;
    }

    private static bool IsUncPath(string fullPath) =>
        fullPath.StartsWith("\\\\", StringComparison.Ordinal) ||
        fullPath.StartsWith("//", StringComparison.Ordinal);
}
