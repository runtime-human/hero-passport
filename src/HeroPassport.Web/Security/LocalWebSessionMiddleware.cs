namespace HeroPassport.Web.Security;

internal sealed class LocalWebSessionMiddleware(
    RequestDelegate next,
    LocalWebSessionAuthority authority)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (IsBootstrapRequest(context.Request))
        {
            await next(context);
            return;
        }

        if (!context.Request.Cookies.TryGetValue(LocalWebSessionAuthority.CookieName, out var token)
            || !authority.IsSessionValid(token))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.CacheControl = "no-store";
            return;
        }

        await next(context);
    }

    private static bool IsBootstrapRequest(HttpRequest request) =>
        (HttpMethods.IsGet(request.Method)
            && request.Path.Equals("/__hero/bootstrap", StringComparison.Ordinal))
        || (HttpMethods.IsPost(request.Method)
            && request.Path.Equals("/__hero/bootstrap/claim", StringComparison.Ordinal));
}
