using Microsoft.AspNetCore.Http;
using ParkingSystem.Parking.WebAPI.Security;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

public sealed class LocationResponsePrivacyMiddlewareTests
{
    [Theory]
    [InlineData("/api/parking/lots/search")]
    [InlineData("/api/parking/lots/nearby")]
    [InlineData("/api/parking/lots/recommendations")]
    [InlineData("/API/PARKING/LOTS/SEARCH")]
    public async Task Gps_read_routes_disable_http_caching_even_for_error_responses(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = path;
        context.Request.QueryString = new QueryString("?latitude=10.751234&longitude=106.691234");
        var middleware = new LocationResponsePrivacyMiddleware(ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.Equal("private, no-store", context.Response.Headers.CacheControl.ToString());
        Assert.Equal("no-cache", context.Response.Headers.Pragma.ToString());
        Assert.Equal("0", context.Response.Headers.Expires.ToString());
    }

    [Theory]
    [InlineData("/api/parking/lots")]
    [InlineData("/api/parking/lots/123")]
    [InlineData("/api/parking/lots/searching")]
    public async Task Other_routes_do_not_change_http_cache_policy(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = path;
        var middleware = new LocationResponsePrivacyMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(context);
        Assert.False(context.Response.Headers.ContainsKey("Cache-Control"));
    }

    [Fact]
    public async Task Non_get_requests_do_not_change_http_cache_policy()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/parking/lots/search";
        var middleware = new LocationResponsePrivacyMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(context);
        Assert.False(context.Response.Headers.ContainsKey("Cache-Control"));
    }
}
