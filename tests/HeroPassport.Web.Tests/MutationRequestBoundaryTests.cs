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
        context.Request.ContentType = contentType;
        context.Request.ContentLength = contentLength;
        context.Request.Body = new MemoryStream();
        context.Features.Set<IHttpMaxRequestBodySizeFeature>(new MutableMaxRequestBodySizeFeature());
        return context;
    }

    private sealed class MutableMaxRequestBodySizeFeature : IHttpMaxRequestBodySizeFeature
    {
        public bool IsReadOnly => false;
        public long? MaxRequestBodySize { get; set; }
    }
}
