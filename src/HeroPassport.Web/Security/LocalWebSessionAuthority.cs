using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Hosting;

namespace HeroPassport.Web.Security;

internal sealed class LocalWebSessionAuthority
{
    internal const int SecretSize = 32;
    internal const string CookieName = ".HeroPassport.LocalSession";

    private readonly byte[] _bootstrap;
    private readonly byte[] _session;
    private int _bootstrapConsumed;

    private LocalWebSessionAuthority(byte[] bootstrap, byte[] session)
    {
        _bootstrap = bootstrap;
        _session = session;
    }

    internal string BootstrapCapability => WebEncoders.Base64UrlEncode(_bootstrap);

    internal string SessionToken => WebEncoders.Base64UrlEncode(_session);

    internal static LocalWebSessionAuthority Create(IHostEnvironment environment)
    {
        var testBootstrap = Environment.GetEnvironmentVariable("HERO_PASSPORT_WEB_TEST_BOOTSTRAP");
        var testSession = Environment.GetEnvironmentVariable("HERO_PASSPORT_WEB_TEST_SESSION");
        var hasTestOverride = testBootstrap is not null || testSession is not null;

        if (hasTestOverride && !environment.IsEnvironment("Testing"))
        {
            throw new InvalidOperationException(
                "Web test secrets are only accepted in the Testing environment.");
        }

        if (environment.IsEnvironment("Testing"))
        {
            if (testBootstrap is null || testSession is null)
            {
                throw new InvalidOperationException(
                    "Testing requires both deterministic Web test secrets.");
            }

            return CreateForTesting(testBootstrap, testSession);
        }

        return new LocalWebSessionAuthority(
            RandomNumberGenerator.GetBytes(SecretSize),
            RandomNumberGenerator.GetBytes(SecretSize));
    }

    internal static LocalWebSessionAuthority CreateForTesting(
        string bootstrapCapability,
        string sessionToken) =>
        new(
            DecodeExactSecret(bootstrapCapability, nameof(bootstrapCapability)),
            DecodeExactSecret(sessionToken, nameof(sessionToken)));

    internal bool TryConsumeBootstrap(string? candidate)
    {
        if (Volatile.Read(ref _bootstrapConsumed) != 0 || !Matches(candidate, _bootstrap))
        {
            return false;
        }

        return Interlocked.CompareExchange(ref _bootstrapConsumed, 1, 0) == 0;
    }

    internal bool IsSessionValid(string? candidate) => Matches(candidate, _session);

    private static byte[] DecodeExactSecret(string encoded, string parameterName)
    {
        byte[] decoded;
        try
        {
            decoded = WebEncoders.Base64UrlDecode(encoded);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("Secret must be valid base64url.", parameterName, exception);
        }

        if (decoded.Length != SecretSize)
        {
            throw new ArgumentException(
                $"Secret must decode to exactly {SecretSize} bytes.",
                parameterName);
        }

        return decoded;
    }

    private static bool Matches(string? encoded, byte[] expected)
    {
        if (string.IsNullOrWhiteSpace(encoded))
        {
            return false;
        }

        byte[] decoded;
        try
        {
            decoded = WebEncoders.Base64UrlDecode(encoded);
        }
        catch (FormatException)
        {
            return false;
        }

        return decoded.Length == SecretSize
            && CryptographicOperations.FixedTimeEquals(decoded, expected);
    }
}
