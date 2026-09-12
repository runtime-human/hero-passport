namespace HeroPassport.Infrastructure.Runtime;

public static class HeroPassportRuntimePaths
{
    public static string ResolveHome()
    {
        var overridden = Environment.GetEnvironmentVariable("HERO_PASSPORT_HOME");
        if (!string.IsNullOrWhiteSpace(overridden))
        {
            return Path.GetFullPath(overridden);
        }

        if (OperatingSystem.IsWindows())
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localAppData))
            {
                throw new InvalidOperationException("LOCALAPPDATA is unavailable.");
            }

            return Path.Combine(localAppData, "HeroPassport");
        }

        if (OperatingSystem.IsMacOS())
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrWhiteSpace(home))
            {
                throw new InvalidOperationException("User home directory is unavailable.");
            }

            return Path.Combine(home, "Library", "Application Support", "HeroPassport");
        }

        var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (!string.IsNullOrWhiteSpace(xdgDataHome))
        {
            return Path.Combine(Path.GetFullPath(xdgDataHome), "hero-passport");
        }

        var unixHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(unixHome))
        {
            throw new InvalidOperationException("User home directory is unavailable.");
        }

        return Path.Combine(unixHome, ".local", "share", "hero-passport");
    }

    public static string ResolveDatabasePath() => Path.Combine(ResolveHome(), "hero-passport.db");
}
