namespace HeroPassport.App;

public static class HeroPassportRuntimePaths
{
    public static string ResolveHome() =>
        Infrastructure.Runtime.HeroPassportRuntimePaths.ResolveHome();

    public static string ResolveDatabasePath() =>
        Infrastructure.Runtime.HeroPassportRuntimePaths.ResolveDatabasePath();
}
