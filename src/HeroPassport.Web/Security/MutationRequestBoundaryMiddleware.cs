using Microsoft.AspNetCore.Http.Features;

namespace HeroPassport.Web.Security;

internal sealed class MutationRequestBoundaryMiddleware(RequestDelegate next)
{
    private const long MaxRequestBodyBytes = 8192;
    private const int MaxFormEntries = 16;
    private const int MaxFormKeyChars = 128;
    private const int MaxFormValueChars = 2048;
    private const string UrlEncodedFormContentType = "application/x-www-form-urlencoded";

    public async Task InvokeAsync(HttpContext context)
    {
        if (!IsQuestMutationPost(context.Request))
        {
            await next(context);
            return;
        }

        if (!IsUrlEncodedForm(context.Request))
        {
            context.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
            return;
        }

        if (context.Request.ContentLength is > MaxRequestBodyBytes)
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

        requestBodySize.MaxRequestBodySize = MaxRequestBodyBytes;
        context.Features.Set<IFormFeature>(
            new FormFeature(
                context.Request,
                new FormOptions
                {
                    BufferBodyLengthLimit = MaxRequestBodyBytes,
                    KeyLengthLimit = MaxFormKeyChars,
                    ValueCountLimit = MaxFormEntries,
                    ValueLengthLimit = MaxFormValueChars,
                }));

        await next(context);
    }

    private static bool IsQuestMutationPost(HttpRequest request)
    {
        if (!HttpMethods.IsPost(request.Method))
        {
            return false;
        }

        var path = request.Path.Value ?? string.Empty;
        return string.Equals(path, "/quests/start", StringComparison.Ordinal)
            || path.StartsWith("/quests/start/confirm/", StringComparison.Ordinal);
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
