namespace HeroPassport.App;

public static class HeroPassportRuntimePaths
{
    public static string ResolveHome()
    {
        var overrideRoot = Environment.GetEnvironmentVariable("HERO_PASSPORT_HOME");
        if (!string.IsNullOrWhiteSpace(overrideRoot))
        {
            return Path.GetFullPath(overrideRoot);
        }

        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HeroPassport");
        }

        if (OperatingSystem.IsMacOS())
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "HeroPassport");
        }

        var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        return !string.IsNullOrWhiteSpace(xdg)
            ? Path.Combine(Path.GetFullPath(xdg), "hero-passport")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "hero-passport");
    }

    public static string ResolveDatabasePath() => Path.Combine(ResolveHome(), "hero-passport.db");
}
