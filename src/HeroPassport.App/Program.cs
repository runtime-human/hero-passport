using HeroPassport.App.Mcp;

if (args.Length == 0 || !string.Equals(args[0], "mcp", StringComparison.Ordinal))
{
    Console.Error.WriteLine("Usage: hero-passport mcp [--project-root <path>]");
    return 2;
}

try
{
    return await HeroPassportMcpHost.RunAsync(args[1..]).ConfigureAwait(false);
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Hero Passport failed to start: {exception.GetType().Name}.");
    return 1;
}
