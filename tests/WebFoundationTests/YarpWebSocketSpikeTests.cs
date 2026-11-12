using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Yarp.ReverseProxy.Configuration;

namespace ParkingSystem.Tests.WebFoundation;

/// <summary>
/// TECH Spike: Verify that YARP Gateway transparently proxies WebSocket connections
/// (HTTP/1.1 Upgrade to WebSocket) to downstream backend microservices (e.g. SignalR hubs).
/// </summary>
public sealed class YarpWebSocketSpikeTests : IAsyncLifetime
{
    private WebApplication _destination = null!;
    private WebApplication _gateway = null!;
    private int _gatewayPort;

    public async ValueTask InitializeAsync()
    {
        // 1. Start downstream WebSocket service on ephemeral port
        _destination = await StartDestinationWebSocketServiceAsync();
        var destinationAddress = _destination.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single();

        // 2. Start YARP Gateway on ephemeral port pointing to destination
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddReverseProxy().LoadFromMemory(
            [
                new RouteConfig
                {
                    RouteId = "notification-hub-ws",
                    ClusterId = "notification-cluster",
                    Match = new RouteMatch
                    {
                        Path = "/api/notifications/{**catch-all}"
                    }
                }
            ],
            [
                new ClusterConfig
                {
                    ClusterId = "notification-cluster",
                    Destinations = new Dictionary<string, DestinationConfig>
                    {
                        ["default"] = new() { Address = destinationAddress }
                    }
                }
            ]);

        _gateway = builder.Build();
        _gateway.MapReverseProxy();
        await _gateway.StartAsync(TestContext.Current.CancellationToken);

        var gatewayAddress = _gateway.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        _gatewayPort = new Uri(gatewayAddress).Port;
    }

    [Fact]
    public async Task Yarp_gateway_transparently_proxies_websocket_traffic_and_preserves_frames()
    {
        using var clientWs = new ClientWebSocket();
        var targetUri = new Uri($"ws://127.0.0.1:{_gatewayPort}/api/notifications/ws/echo");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await clientWs.ConnectAsync(targetUri, cts.Token);

        Assert.Equal(WebSocketState.Open, clientWs.State);

        // Send a frame through YARP
        const string payload = "Hello-YARP-WebSocket-Spike!";
        var sendBuffer = Encoding.UTF8.GetBytes(payload);
        await clientWs.SendAsync(
            new ArraySegment<byte>(sendBuffer),
            WebSocketMessageType.Text,
            endOfMessage: true,
            cts.Token);

        // Receive the echoed frame forwarded back through YARP
        var receiveBuffer = new byte[1024];
        var result = await clientWs.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), cts.Token);

        Assert.Equal(WebSocketMessageType.Text, result.MessageType);
        Assert.True(result.EndOfMessage);

        var receivedMessage = Encoding.UTF8.GetString(receiveBuffer, 0, result.Count);
        Assert.Equal(payload, receivedMessage);

        // Clean close through YARP
        await clientWs.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing test", cts.Token);
        Assert.Equal(WebSocketState.Closed, clientWs.State);
    }

    private static async Task<WebApplication> StartDestinationWebSocketServiceAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();

        app.UseWebSockets();
        app.Map("/api/notifications/ws/echo", async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                return;
            }

            using var ws = await context.WebSockets.AcceptWebSocketAsync();
            var buffer = new byte[1024];
            while (ws.State == WebSocketState.Open)
            {
                var receiveResult = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), context.RequestAborted);
                if (receiveResult.MessageType == WebSocketMessageType.Close)
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", context.RequestAborted);
                    break;
                }

                await ws.SendAsync(
                    new ArraySegment<byte>(buffer, 0, receiveResult.Count),
                    receiveResult.MessageType,
                    receiveResult.EndOfMessage,
                    context.RequestAborted);
            }
        });

        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    public async ValueTask DisposeAsync()
    {
        await _gateway.DisposeAsync();
        await _destination.DisposeAsync();
    }
}
