namespace HeroPassport.Web.Security;

internal static class LocalWebOriginPolicy
{
    internal static bool IsAllowed(HttpRequest request)
    {
        if (request.Headers.TryGetValue("Sec-Fetch-Site", out var fetchSite)
            && fetchSite.Count == 1
            && fetchSite[0] is not ("same-origin" or "none"))
        {
            return false;
        }

        if (!request.Headers.TryGetValue("Origin", out var origin) || origin.Count == 0)
        {
            return true;
        }

        if (origin.Count != 1 || request.Host.Port is null)
        {
            return false;
        }

        var expected = $"http://127.0.0.1:{request.Host.Port.Value}";
        return string.Equals(origin[0], expected, StringComparison.Ordinal);
    }
}
