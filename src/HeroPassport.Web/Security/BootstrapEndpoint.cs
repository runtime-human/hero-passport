using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.Features;

namespace HeroPassport.Web.Security;

internal static class BootstrapEndpoint
{
    private const int MaxCapabilityChars = 128;
    private const long MaxClaimBodyBytes = 1024;
    private const int MaxFormEntries = 4;
    private const int MaxFormKeyChars = 64;
    private const int MaxFormValueChars = 512;
    private const string UrlEncodedFormContentType = "application/x-www-form-urlencoded";

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

        if (!IsUrlEncodedForm(context.Request))
        {
            context.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
            return;
        }

        if (context.Request.ContentLength is > MaxClaimBodyBytes)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
        }

        var requestBodySize = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (requestBodySize is null || requestBodySize.IsReadOnly)
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return;
        }

        requestBodySize.MaxRequestBodySize = MaxClaimBodyBytes;
        context.Features.Set<IFormFeature>(
            new FormFeature(
                context.Request,
                new FormOptions
                {
                    BufferBodyLengthLimit = MaxClaimBodyBytes,
                    KeyLengthLimit = MaxFormKeyChars,
                    ValueCountLimit = MaxFormEntries,
                    ValueLengthLimit = MaxFormValueChars,
                }));

        try
        {
            await antiforgery.ValidateRequestAsync(context);
        }
        catch (AntiforgeryValidationException)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }
        catch (BadHttpRequestException exception)
            when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
        }
        catch (InvalidDataException)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
        }

        IFormCollection form;
        try
        {
            form = await context.Request.ReadFormAsync(context.RequestAborted);
        }
        catch (BadHttpRequestException exception)
            when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
        }
        catch (InvalidDataException)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
        }

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

    private static bool IsUrlEncodedForm(HttpRequest request)
    {
        var contentType = request.ContentType;
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        var parameterSeparator = contentType.IndexOf(';', StringComparison.Ordinal);
        var mediaType = parameterSeparator >= 0
            ? contentType[..parameterSeparator].Trim()
            : contentType.Trim();
        return string.Equals(
            mediaType,
            UrlEncodedFormContentType,
            StringComparison.OrdinalIgnoreCase);
    }
}
