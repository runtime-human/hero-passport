namespace HeroPassport.Web.Security;

internal sealed class BootstrapResponseHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (HttpMethods.IsGet(context.Request.Method)
            && context.Request.Path.Equals("/__hero/bootstrap", StringComparison.Ordinal))
        {
            context.Response.OnStarting(static state =>
            {
                var response = (HttpResponse)state;
                response.Headers.CacheControl = "no-store";
                response.Headers.ReferrerPolicy = "no-referrer";
                response.Headers.XContentTypeOptions = "nosniff";
                return Task.CompletedTask;
            }, context.Response);
        }

        await next(context);
    }
}
