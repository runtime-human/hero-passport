using HeroPassport.Web.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Xunit;

namespace HeroPassport.Web.Tests;

public sealed class MutationRequestBoundaryTests
{
    [Fact]
    public async Task MultipartStartPostIsRejectedBeforeNextDelegate()
    {
        var nextCalled = false;
        var middleware = new MutationRequestBoundaryMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = Context("POST", "/quests/start", "multipart/form-data; boundary=x", 128);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, context.Response.StatusCode);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task OversizedStartPostIsRejectedBeforeNextDelegate()
    {
        var nextCalled = false;
        var middleware = new MutationRequestBoundaryMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = Context("POST", "/quests/start", "application/x-www-form-urlencoded", 8193);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status413PayloadTooLarge, context.Response.StatusCode);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task FinishPostUsesDedicatedThirtyTwoKiBBoundary()
    {
        var nextCalled = false;
        var middleware = new MutationRequestBoundaryMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = Context(
            "POST",
            $"/quests/finish/{Guid.CreateVersion7():D}",
            "application/x-www-form-urlencoded",
            24_576);

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.Equal(32_768, context.Features.Get<IHttpMaxRequestBodySizeFeature>()!.MaxRequestBodySize);
    }

    [Fact]
    public async Task MaximumEncodedUnicodeFinishValuePassesBoundedFormParser()
    {
        var summary = string.Concat(Enumerable.Repeat("🚀", 2000));
        using var content = new FormUrlEncodedContent([new("summary", summary)]);
        var body = await content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        Assert.InRange(body.Length, 24_000, 32_768);

        string? parsed = null;
        var middleware = new MutationRequestBoundaryMiddleware(async context =>
        {
            var form = await context.Request.ReadFormAsync(TestContext.Current.CancellationToken);
            parsed = form["summary"];
        });
        var context = Context(
            "POST",
            $"/quests/finish/{Guid.CreateVersion7():D}",
            content.Headers.ContentType!.ToString(),
            body.Length);
        context.Request.Body = new MemoryStream(body);

        await middleware.InvokeAsync(context);

        Assert.Equal(summary, parsed);
    }

    [Fact]
    public async Task FinishFormFloodIsRejectedByConfiguredParser()
    {
        var values = Enumerable.Range(0, 17)
            .Select(index => new KeyValuePair<string, string>($"k{index}", "v"))
            .ToArray();
        using var content = new FormUrlEncodedContent(values);
        var body = await content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        var nextCalled = false;
        var middleware = new MutationRequestBoundaryMiddleware(async context =>
        {
            nextCalled = true;
            _ = await context.Request.ReadFormAsync(TestContext.Current.CancellationToken);
        });
        var context = Context(
            "POST",
            $"/quests/finish/{Guid.CreateVersion7():D}",
            content.Headers.ContentType!.ToString(),
            body.Length);
        context.Request.Body = new MemoryStream(body);

        await Assert.ThrowsAsync<InvalidDataException>(() => middleware.InvokeAsync(context));

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task OversizedFinishPostIsRejectedBeforeNextDelegate()
    {
        var nextCalled = false;
        var middleware = new MutationRequestBoundaryMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = Context(
            "POST",
            $"/quests/finish/confirm/abcdefghijklmnopqrstuv",
            "application/x-www-form-urlencoded",
            32_769);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status413PayloadTooLarge, context.Response.StatusCode);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task CrossSiteAndMissingBrowserProvenanceAreRejectedBeforeNextDelegate()
    {
        var calls = 0;
        var middleware = new MutationRequestBoundaryMiddleware(_ =>
        {
            calls++;
            return Task.CompletedTask;
        });

        var crossSite = Context("POST", "/quests/start", "application/x-www-form-urlencoded", 64);
        crossSite.Request.Headers["Sec-Fetch-Site"] = "cross-site";
        crossSite.Request.Headers["Origin"] = "https://attacker.example";
        await middleware.InvokeAsync(crossSite);

        var missing = Context("POST", "/quests/start/confirm/abc", "application/x-www-form-urlencoded", 64);
        missing.Request.Headers.Remove("Sec-Fetch-Site");
        missing.Request.Headers.Remove("Origin");
        await middleware.InvokeAsync(missing);

        Assert.Equal(StatusCodes.Status400BadRequest, crossSite.Response.StatusCode);
        Assert.Equal(StatusCodes.Status400BadRequest, missing.Response.StatusCode);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task SmallUrlEncodedConfirmPostReachesNextWithBoundedBodyFeature()
    {
        var nextCalled = false;
        var middleware = new MutationRequestBoundaryMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = Context(
            "POST",
            "/quests/start/confirm/abc",
            "application/x-www-form-urlencoded; charset=utf-8",
            64);

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.Equal(8192, context.Features.Get<IHttpMaxRequestBodySizeFeature>()!.MaxRequestBodySize);
    }

    [Fact]
    public async Task NonPostAndUnrelatedRoutesAreNotIntercepted()
    {
        var calls = 0;
        var middleware = new MutationRequestBoundaryMiddleware(_ =>
        {
            calls++;
            return Task.CompletedTask;
        });

        var getStart = Context("GET", "/quests/start", null, null);
        await middleware.InvokeAsync(getStart);
        var unrelatedPost = Context("POST", "/unrelated", "multipart/form-data; boundary=x", 9000);
        await middleware.InvokeAsync(unrelatedPost);

        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task BootstrapClaimKeepsItsOwnRequestBoundary()
    {
        var nextCalled = false;
        var middleware = new MutationRequestBoundaryMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = Context(
            "POST",
            "/__hero/bootstrap/claim",
            "multipart/form-data; boundary=x",
            9000);

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.Null(context.Features.Get<IHttpMaxRequestBodySizeFeature>()!.MaxRequestBodySize);
    }

    private static DefaultHttpContext Context(
        string method,
        string path,
        string? contentType,
        long? contentLength)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("127.0.0.1", 54321);
        context.Request.ContentType = contentType;
        context.Request.ContentLength = contentLength;
        context.Request.Body = new MemoryStream();
        context.Request.Headers["Sec-Fetch-Site"] = "same-origin";
        context.Request.Headers["Origin"] = "http://127.0.0.1:54321";
        context.Features.Set<IHttpMaxRequestBodySizeFeature>(new MutableMaxRequestBodySizeFeature());
        return context;
    }

    private sealed class MutableMaxRequestBodySizeFeature : IHttpMaxRequestBodySizeFeature
    {
        public bool IsReadOnly => false;
        public long? MaxRequestBodySize { get; set; }
    }
}
