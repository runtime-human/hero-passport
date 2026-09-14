using Microsoft.AspNetCore.Http.Features;

namespace HeroPassport.Web.Security;

internal sealed class MutationRequestBoundaryMiddleware(RequestDelegate next)
{
    private const long StartMaxRequestBodyBytes = 8192;
    private const int StartMaxFormValueBytes = 2048;
    private const long FinishMaxRequestBodyBytes = 32768;
    private const int FinishMaxFormValueBytes = 24 * 1024;
    private const int MaxFormEntries = 16;
    private const int MaxFormKeyBytes = 128;
    private const string UrlEncodedFormContentType = "application/x-www-form-urlencoded";

    public async Task InvokeAsync(HttpContext context)
    {
        var limits = GetMutationLimits(context.Request);
        if (limits is null)
        {
            await next(context);
            return;
        }

        if (!IsUrlEncodedForm(context.Request))
        {
            context.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
            return;
        }

        if (context.Request.ContentLength is > 0
            && context.Request.ContentLength > limits.Value.MaxRequestBodyBytes)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
        }

        if (!HasSameOriginBrowserProvenance(context.Request))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var requestBodySize = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (requestBodySize is null || requestBodySize.IsReadOnly)
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return;
        }

        requestBodySize.MaxRequestBodySize = limits.Value.MaxRequestBodyBytes;
        context.Features.Set<IFormFeature>(
            new FormFeature(
                context.Request,
                new FormOptions
                {
                    BufferBodyLengthLimit = limits.Value.MaxRequestBodyBytes,
                    KeyLengthLimit = MaxFormKeyBytes,
                    ValueCountLimit = MaxFormEntries,
                    ValueLengthLimit = limits.Value.MaxFormValueBytes,
                }));

        await next(context);
    }

    private static MutationLimits? GetMutationLimits(HttpRequest request)
    {
        if (!HttpMethods.IsPost(request.Method))
        {
            return null;
        }

        var path = request.Path.Value ?? string.Empty;
        if (string.Equals(path, "/quests/start", StringComparison.Ordinal)
            || HasSingleSegmentAfter(path, "/quests/start/confirm/"))
        {
            return new(StartMaxRequestBodyBytes, StartMaxFormValueBytes);
        }

        if (HasSingleSegmentAfter(path, "/quests/finish/confirm/")
            || HasSingleSegmentAfter(path, "/quests/finish/"))
        {
            return new(FinishMaxRequestBodyBytes, FinishMaxFormValueBytes);
        }

        return null;
    }

    private static bool HasSingleSegmentAfter(string path, string prefix)
    {
        if (!path.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var remainder = path[prefix.Length..];
        return remainder.Length > 0 && !remainder.Contains('/', StringComparison.Ordinal);
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

    private static bool HasSameOriginBrowserProvenance(HttpRequest request)
    {
        var hasFetchSite = request.Headers.TryGetValue("Sec-Fetch-Site", out var fetchSiteValues);
        if (hasFetchSite
            && (fetchSiteValues.Count != 1
                || !string.Equals(fetchSiteValues[0], "same-origin", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var hasOrigin = request.Headers.TryGetValue("Origin", out var originValues);
        if (hasOrigin && !IsCanonicalOrigin(request, originValues))
        {
            return false;
        }

        return hasFetchSite || hasOrigin;
    }

    private static bool IsCanonicalOrigin(
        HttpRequest request,
        Microsoft.Extensions.Primitives.StringValues originValues)
    {
        if (originValues.Count != 1
            || !Uri.TryCreate(originValues[0], UriKind.Absolute, out var origin))
        {
            return false;
        }

        var expectedPort = request.Host.Port ?? request.Scheme switch
        {
            "http" => 80,
            "https" => 443,
            _ => -1,
        };
        return expectedPort >= 0
            && string.Equals(origin.Scheme, request.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(origin.Host, request.Host.Host, StringComparison.OrdinalIgnoreCase)
            && origin.Port == expectedPort
            && string.IsNullOrEmpty(origin.UserInfo)
            && origin.AbsolutePath == "/"
            && string.IsNullOrEmpty(origin.Query)
            && string.IsNullOrEmpty(origin.Fragment);
    }

    private readonly record struct MutationLimits(long MaxRequestBodyBytes, int MaxFormValueBytes);
}
