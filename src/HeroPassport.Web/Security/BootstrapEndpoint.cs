using Microsoft.AspNetCore.Antiforgery;

namespace HeroPassport.Web.Security;

internal static class BootstrapEndpoint
{
    private const int MaxCapabilityChars = 128;

    internal static IEndpointConventionBuilder MapBootstrapClaim(
        this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost("/__hero/bootstrap/claim", HandleAsync);

    private static async Task HandleAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        LocalWebSessionAuthority authority)
    {
        context.Response.Headers.CacheControl = "no-store";

        if (!LocalWebOriginPolicy.IsAllowed(context.Request))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        try
        {
            await antiforgery.ValidateRequestAsync(context);
        }
        catch (AntiforgeryValidationException)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        if (!context.Request.HasFormContentType)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var form = await context.Request.ReadFormAsync(context.RequestAborted);
        var values = form["capability"];
        var capability = values.Count == 1 ? values[0] : null;

        if (capability is null
            || capability.Length > MaxCapabilityChars
            || !authority.TryConsumeBootstrap(capability))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        context.Response.Cookies.Append(
            LocalWebSessionAuthority.CookieName,
            authority.SessionToken,
            new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                Secure = false,
                Path = "/",
                IsEssential = true,
            });
        context.Response.StatusCode = StatusCodes.Status303SeeOther;
        context.Response.Headers.Location = "/";
    }
}
