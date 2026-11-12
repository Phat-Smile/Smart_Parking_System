using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ParkingSystem.AspNetCore;
using ParkingSystem.AspNetCore.Security;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.Parking.Application.Common.Realtime;
using ParkingSystem.Parking.WebAPI.Hubs;
using ParkingSystem.Parking.WebAPI.Realtime;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public sealed class ParkingHubAccessControlTests
{
    private const string Issuer = "ParkingSystem.Identity";
    private const string Audience = "ParkingSystem";
    private const string ServiceKey = "parking-hub-test-service-key";

    private static readonly Guid AssignedLotId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherLotId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid SlotId = Guid.Parse("20000000-0000-0000-0000-000000000001");

    private static readonly Guid StaffAccountId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OwnerAccountId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid AdminAccountId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid CustomerAccountId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    [Fact]
    public void Hub_methods_have_expected_authorize_attributes()
    {
        var joinPublic = typeof(ParkingHub).GetMethod(nameof(ParkingHub.JoinLotGroup));
        var leavePublic = typeof(ParkingHub).GetMethod(nameof(ParkingHub.LeaveLotGroup));
        var joinOps = typeof(ParkingHub).GetMethod("JoinLotOperationsGroup");
        var leaveOps = typeof(ParkingHub).GetMethod("LeaveLotOperationsGroup");

        Assert.NotNull(joinPublic);
        Assert.NotNull(leavePublic);
        Assert.Null(joinPublic.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Null(leavePublic.GetCustomAttribute<AuthorizeAttribute>());

        Assert.NotNull(joinOps);
        Assert.NotNull(leaveOps);
        Assert.NotNull(joinOps.GetCustomAttribute<AuthorizeAttribute>());
        Assert.NotNull(leaveOps.GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public async Task Notifier_routes_occupancy_to_public_group_and_slot_events_to_ops_group()
    {
        var fakeHubContext = new FakeHubContext();
        var notifier = new SignalRParkingRealtimeNotifier(fakeHubContext, NullLogger<SignalRParkingRealtimeNotifier>.Instance);
        var now = DateTimeOffset.Parse("2026-11-11T10:00:00Z");

        await notifier.NotifyLotOccupancyChangedAsync(AssignedLotId, 5, 10, now, TestContext.Current.CancellationToken);
        await notifier.NotifySlotStatusChangedAsync(AssignedLotId, SlotId, "A-01", "Occupied", "Available", now, TestContext.Current.CancellationToken);
        await notifier.NotifySlotNeedsVerificationAsync(AssignedLotId, SlotId, "A-01", "Staff", now, TestContext.Current.CancellationToken);

        var occupancyCall = Assert.Single(fakeHubContext.ClientsFake.GroupClient.OccupancyMessages);
        Assert.Equal($"lot_{AssignedLotId}_public", occupancyCall.GroupName);
        Assert.Equal((AssignedLotId, 5, 10, now), (occupancyCall.Message.LotId, occupancyCall.Message.AvailableCarSlots, occupancyCall.Message.AvailableMotorbikeSlots, occupancyCall.Message.Timestamp));

        var slotChangedCall = Assert.Single(fakeHubContext.ClientsFake.GroupClient.SlotStatusMessages);
        Assert.Equal($"lot_{AssignedLotId}_ops", slotChangedCall.GroupName);
        Assert.Equal("A-01", slotChangedCall.Message.SlotCode);

        var verificationCall = Assert.Single(fakeHubContext.ClientsFake.GroupClient.VerificationMessages);
        Assert.Equal($"lot_{AssignedLotId}_ops", verificationCall.GroupName);
        Assert.Equal("Staff", verificationCall.Message.Stage);
    }

    [Fact]
    public async Task Anonymous_client_can_join_public_group_and_receives_only_lot_occupancy_changed()
    {
        using var keys = new TestJwtKeys();
        await using var app = await CreateHubHostAsync(keys.PublicKeyPath);
        var notifier = app.Services.GetRequiredService<IParkingRealtimeNotifier>();
        var now = DateTimeOffset.Parse("2026-11-11T10:05:00Z");

        await using var client = await ConnectSignalRAsync(app, token: null);
        var joinCompletion = await client.InvokeAsync("JoinLotGroup", [AssignedLotId], TestContext.Current.CancellationToken);
        Assert.False(joinCompletion.TryGetProperty("error", out _));

        // Send operational events BEFORE occupancy event: if public group received either ops event,
        // it would arrive before LotOccupancyChanged.
        await notifier.NotifySlotStatusChangedAsync(AssignedLotId, SlotId, "A-01", "Occupied", "Available", now, TestContext.Current.CancellationToken);
        await notifier.NotifySlotNeedsVerificationAsync(AssignedLotId, SlotId, "A-01", "Staff", now, TestContext.Current.CancellationToken);
        await notifier.NotifyLotOccupancyChangedAsync(AssignedLotId, 4, 8, now, TestContext.Current.CancellationToken);

        var firstInvocation = await client.ReceiveInvocationAsync(TestContext.Current.CancellationToken);
        Assert.Equal("LotOccupancyChanged", firstInvocation.GetProperty("target").GetString());
        var payload = firstInvocation.GetProperty("arguments")[0];
        Assert.Equal(AssignedLotId, payload.GetProperty("lotId").GetGuid());
        Assert.Equal(4, payload.GetProperty("availableCarSlots").GetInt32());
        Assert.Equal(8, payload.GetProperty("availableMotorbikeSlots").GetInt32());
    }

    [Fact]
    public async Task Anonymous_client_is_rejected_from_joining_or_leaving_operations_group()
    {
        using var keys = new TestJwtKeys();
        await using var app = await CreateHubHostAsync(keys.PublicKeyPath);
        var notifier = app.Services.GetRequiredService<IParkingRealtimeNotifier>();
        var now = DateTimeOffset.Parse("2026-11-11T10:06:00Z");

        await using var client = await ConnectSignalRAsync(app, token: null);

        var joinOpsCompletion = await client.InvokeAsync("JoinLotOperationsGroup", [AssignedLotId], TestContext.Current.CancellationToken);
        Assert.True(joinOpsCompletion.TryGetProperty("error", out var joinError));
        Assert.False(string.IsNullOrWhiteSpace(joinError.GetString()));

        var leaveOpsCompletion = await client.InvokeAsync("LeaveLotOperationsGroup", [AssignedLotId], TestContext.Current.CancellationToken);
        Assert.True(leaveOpsCompletion.TryGetProperty("error", out var leaveError));
        Assert.False(string.IsNullOrWhiteSpace(leaveError.GetString()));

        // Join public afterwards and confirm ops events are not delivered.
        var joinPublicCompletion = await client.InvokeAsync("JoinLotGroup", [AssignedLotId], TestContext.Current.CancellationToken);
        Assert.False(joinPublicCompletion.TryGetProperty("error", out _));

        await notifier.NotifySlotStatusChangedAsync(AssignedLotId, SlotId, "A-01", "Occupied", "Available", now, TestContext.Current.CancellationToken);
        await notifier.NotifyLotOccupancyChangedAsync(AssignedLotId, 3, 7, now, TestContext.Current.CancellationToken);

        var firstInvocation = await client.ReceiveInvocationAsync(TestContext.Current.CancellationToken);
        Assert.Equal("LotOccupancyChanged", firstInvocation.GetProperty("target").GetString());
    }

    [Theory]
    [InlineData("Staff", "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")]
    [InlineData("ParkingLotOwner", "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")]
    [InlineData("SystemAdmin", "cccccccc-cccc-cccc-cccc-cccccccccccc")]
    public async Task Authorized_operator_or_admin_can_join_operations_group_via_query_token_and_receives_slot_events(
        string role,
        string accountId)
    {
        using var keys = new TestJwtKeys();
        var logs = new CapturingLoggerProvider();
        await using var app = await CreateHubHostAsync(keys.PublicKeyPath, logs);
        var notifier = app.Services.GetRequiredService<IParkingRealtimeNotifier>();
        var now = DateTimeOffset.Parse("2026-11-11T10:10:00Z");
        var jwt = CreateToken(keys.PrivateKey, accountId, role);

        await using var client = await ConnectSignalRAsync(app, jwt);
        var joinOpsCompletion = await client.InvokeAsync("JoinLotOperationsGroup", [AssignedLotId], TestContext.Current.CancellationToken);
        Assert.False(joinOpsCompletion.TryGetProperty("error", out _));

        // Send occupancy BEFORE slot events: ops group must NOT receive LotOccupancyChanged.
        await notifier.NotifyLotOccupancyChangedAsync(AssignedLotId, 2, 6, now, TestContext.Current.CancellationToken);
        await notifier.NotifySlotStatusChangedAsync(AssignedLotId, SlotId, "A-01", "Occupied", "Available", now, TestContext.Current.CancellationToken);
        await notifier.NotifySlotNeedsVerificationAsync(AssignedLotId, SlotId, "A-01", "Staff", now, TestContext.Current.CancellationToken);

        var first = await client.ReceiveInvocationAsync(TestContext.Current.CancellationToken);
        Assert.Equal("SlotStatusChanged", first.GetProperty("target").GetString());
        Assert.Equal("A-01", first.GetProperty("arguments")[0].GetProperty("slotCode").GetString());
        Assert.Equal("Occupied", first.GetProperty("arguments")[0].GetProperty("effectiveStatus").GetString());

        var second = await client.ReceiveInvocationAsync(TestContext.Current.CancellationToken);
        Assert.Equal("SlotNeedsVerification", second.GetProperty("target").GetString());
        Assert.Equal("Staff", second.GetProperty("arguments")[0].GetProperty("stage").GetString());

        var leaveOpsCompletion = await client.InvokeAsync("LeaveLotOperationsGroup", [AssignedLotId], TestContext.Current.CancellationToken);
        Assert.False(leaveOpsCompletion.TryGetProperty("error", out _));

        var logText = string.Join(Environment.NewLine, logs.Messages);
        Assert.DoesNotContain(jwt, logText, StringComparison.Ordinal);
        Assert.DoesNotContain("access_token=", logText, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Staff", "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "10000000-0000-0000-0000-000000000002")]
    [InlineData("ParkingLotOwner", "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", "10000000-0000-0000-0000-000000000002")]
    [InlineData("Customer", "dddddddd-dddd-dddd-dddd-dddddddddddd", "10000000-0000-0000-0000-000000000001")]
    [InlineData("Staff", "not-a-guid", "10000000-0000-0000-0000-000000000001")]
    [InlineData("SystemAdmin", "cccccccc-cccc-cccc-cccc-cccccccccccc", "00000000-0000-0000-0000-000000000000")]
    public async Task Unassigned_operator_customer_invalid_subject_or_empty_lot_is_rejected_from_operations_group(
        string role,
        string subject,
        string targetLotId)
    {
        using var keys = new TestJwtKeys();
        await using var app = await CreateHubHostAsync(keys.PublicKeyPath);
        var notifier = app.Services.GetRequiredService<IParkingRealtimeNotifier>();
        var lotId = Guid.Parse(targetLotId);
        var now = DateTimeOffset.Parse("2026-11-11T10:15:00Z");
        var jwt = CreateToken(keys.PrivateKey, subject, role);

        await using var client = await ConnectSignalRAsync(app, jwt);
        var joinOpsCompletion = await client.InvokeAsync("JoinLotOperationsGroup", [lotId], TestContext.Current.CancellationToken);
        Assert.True(joinOpsCompletion.TryGetProperty("error", out var error));
        Assert.False(string.IsNullOrWhiteSpace(error.GetString()));

        if (lotId != Guid.Empty)
        {
            var joinPublicCompletion = await client.InvokeAsync("JoinLotGroup", [lotId], TestContext.Current.CancellationToken);
            Assert.False(joinPublicCompletion.TryGetProperty("error", out _));

            await notifier.NotifySlotStatusChangedAsync(lotId, SlotId, "A-01", "Occupied", "Available", now, TestContext.Current.CancellationToken);
            await notifier.NotifyLotOccupancyChangedAsync(lotId, 9, 18, now, TestContext.Current.CancellationToken);

            var first = await client.ReceiveInvocationAsync(TestContext.Current.CancellationToken);
            Assert.Equal("LotOccupancyChanged", first.GetProperty("target").GetString());
        }
    }

    [Fact]
    public async Task Direct_hub_invocation_reads_sub_claim_even_when_user_identifier_is_null()
    {
        var groupManager = new RecordingGroupManager();
        var services = new ServiceCollection();
        services.AddScoped<IParkingLotAccess>(_ => new TestParkingLotAccess());
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var hub = ActivatorUtilities.CreateInstance<ParkingHub>(scope.ServiceProvider);
        hub.Groups = groupManager;
        hub.Context = new FakeHubCallerContext(
            "conn-1",
            new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("sub", AdminAccountId.ToString()), new Claim("role", "SystemAdmin")],
                "Bearer")),
            userIdentifier: null,
            TestContext.Current.CancellationToken);

        var joinOps = typeof(ParkingHub).GetMethod("JoinLotOperationsGroup");
        Assert.NotNull(joinOps);
        await (Task)joinOps.Invoke(hub, [OtherLotId])!;

        var added = Assert.Single(groupManager.Added);
        Assert.Equal(("conn-1", $"lot_{OtherLotId}_ops"), added);
    }

    private static async Task<WebApplication> CreateHubHostAsync(
        string publicKeyPath,
        CapturingLoggerProvider? logs = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseDefaultServiceProvider(options =>
        {
            options.ValidateScopes = true;
            options.ValidateOnBuild = true;
        });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Security:Jwt:Issuer"] = Issuer,
            ["Security:Jwt:Audience"] = Audience,
            ["Security:Jwt:PublicKeyPath"] = publicKeyPath,
            ["Security:Jwt:ClockSkewSeconds"] = "30",
            [$"{ServiceKeyOptions.SectionName}:ServiceKey"] = ServiceKey
        });
        builder.AddParkingSystemWebFoundation();
        builder.AddParkingSystemSecurity();
        builder.Services.AddSignalR();
        builder.Services.AddScoped<IParkingLotAccess>(_ => new TestParkingLotAccess());
        builder.Services.AddSingleton<IParkingRealtimeNotifier, SignalRParkingRealtimeNotifier>();
        if (logs is not null)
            builder.Logging.AddProvider(logs);

        var app = builder.Build();
        app.UseParkingSystemWebFoundation();
        app.UseParkingSystemServiceSecurity();
        app.MapHub<ParkingHub>("/hubs/parking");
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static async Task<SignalRTestClient> ConnectSignalRAsync(WebApplication app, string? token)
    {
        var ct = TestContext.Current.CancellationToken;
        var httpClient = app.GetTestClient();
        var negotiateUri = token is null
            ? "/hubs/parking/negotiate?negotiateVersion=1"
            : $"/hubs/parking/negotiate?negotiateVersion=1&access_token={Uri.EscapeDataString(token)}";
        using var negotiateRequest = new HttpRequestMessage(HttpMethod.Post, negotiateUri);
        negotiateRequest.Headers.Add(ServiceKeyValidationMiddleware.HeaderName, ServiceKey);
        using var negotiateResponse = await httpClient.SendAsync(negotiateRequest, ct);
        Assert.Equal(HttpStatusCode.OK, negotiateResponse.StatusCode);

        var negotiateJson = await negotiateResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
        var connectionToken = negotiateJson.GetProperty("connectionToken").GetString()!;

        var wsClient = app.GetTestServer().CreateWebSocketClient();
        wsClient.ConfigureRequest = request =>
        {
            request.Headers[ServiceKeyValidationMiddleware.HeaderName] = ServiceKey;
        };

        var wsUriString = $"ws://localhost/hubs/parking?id={Uri.EscapeDataString(connectionToken)}";
        if (token is not null)
            wsUriString += $"&access_token={Uri.EscapeDataString(token)}";

        var ws = await wsClient.ConnectAsync(new Uri(wsUriString), ct);
        var client = new SignalRTestClient(ws);
        await client.HandshakeAsync(ct);
        return client;
    }

    private static string CreateToken(RSA signingKey, string subject, string role)
    {
        var now = DateTimeOffset.UtcNow;
        var payload = new Dictionary<string, object>
        {
            ["iss"] = Issuer,
            ["aud"] = Audience,
            ["sub"] = subject,
            ["role"] = role,
            ["nbf"] = now.AddMinutes(-1).ToUnixTimeSeconds(),
            ["exp"] = now.AddMinutes(5).ToUnixTimeSeconds()
        };

        var headerBase64 = Base64Url(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { alg = "RS256", typ = "JWT" })));
        var payloadBase64 = Base64Url(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)));
        var signingInput = $"{headerBase64}.{payloadBase64}";
        var signature = signingKey.SignData(Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{signingInput}.{Base64Url(signature)}";
    }

    private static string Base64Url(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed class TestParkingLotAccess : IParkingLotAccess
    {
        public Task<bool> CanOperateAsync(Guid accountId, Guid lotId, CancellationToken cancellationToken)
        {
            // Intentionally grant CustomerAccountId in DB lookup to prove Hub also checks Staff/Owner role claim.
            var allowed = lotId == AssignedLotId &&
                          (accountId == StaffAccountId || accountId == OwnerAccountId || accountId == CustomerAccountId);
            return Task.FromResult(allowed);
        }
    }

    private sealed class SignalRTestClient(WebSocket socket) : IAsyncDisposable
    {
        private const char RecordSeparator = '\u001e';
        private readonly Queue<JsonElement> _pendingFrames = new();
        private readonly StringBuilder _buffer = new();
        private int _invocationCounter;

        public async Task HandshakeAsync(CancellationToken ct)
        {
            await SendRawAsync("{\"protocol\":\"json\",\"version\":1}", ct);
            var response = await ReadNextFrameAsync(ct);
            Assert.False(response.TryGetProperty("error", out _));
        }

        public async Task<JsonElement> InvokeAsync(string target, object?[] arguments, CancellationToken ct)
        {
            var id = (++_invocationCounter).ToString();
            var frame = JsonSerializer.Serialize(new
            {
                type = 1,
                invocationId = id,
                target,
                arguments
            });
            await SendRawAsync(frame, ct);

            while (true)
            {
                var message = await ReadNextFrameAsync(ct);
                if (message.TryGetProperty("type", out var typeElement) &&
                    typeElement.GetInt32() == 3 &&
                    message.TryGetProperty("invocationId", out var invId) &&
                    invId.GetString() == id)
                {
                    return message;
                }
            }
        }

        public async Task<JsonElement> ReceiveInvocationAsync(CancellationToken ct)
        {
            while (true)
            {
                var message = await ReadNextFrameAsync(ct);
                if (message.TryGetProperty("type", out var typeElement) && typeElement.GetInt32() == 1)
                    return message;
            }
        }

        private async Task SendRawAsync(string json, CancellationToken ct)
        {
            var bytes = Encoding.UTF8.GetBytes(json + RecordSeparator);
            await socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct);
        }

        private async Task<JsonElement> ReadNextFrameAsync(CancellationToken ct)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);

            while (true)
            {
                if (_pendingFrames.Count > 0)
                {
                    var next = _pendingFrames.Dequeue();
                    if (next.TryGetProperty("type", out var typeProp) && typeProp.GetInt32() == 6)
                        continue; // Skip SignalR Ping frames.
                    return next;
                }

                var chunk = new byte[4096];
                var result = await socket.ReceiveAsync(chunk, linked.Token);
                if (result.MessageType == WebSocketMessageType.Close)
                    throw new InvalidOperationException("WebSocket closed unexpectedly.");

                _buffer.Append(Encoding.UTF8.GetString(chunk, 0, result.Count));
                var text = _buffer.ToString();
                int separatorIndex;
                var consumed = 0;
                while ((separatorIndex = text.IndexOf(RecordSeparator, consumed)) >= 0)
                {
                    var rawFrame = text[consumed..separatorIndex];
                    consumed = separatorIndex + 1;
                    if (!string.IsNullOrWhiteSpace(rawFrame))
                    {
                        using var doc = JsonDocument.Parse(rawFrame);
                        _pendingFrames.Enqueue(doc.RootElement.Clone());
                    }
                }

                if (consumed > 0)
                {
                    _buffer.Remove(0, consumed);
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                try
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "test-done", timeout.Token);
                }
                catch (OperationCanceledException)
                {
                }
                catch (WebSocketException)
                {
                }
            }

            socket.Dispose();
        }
    }

    private sealed class FakeHubContext : IHubContext<ParkingHub, IParkingHubClient>
    {
        public FakeHubClients ClientsFake { get; } = new();
        public IHubClients<IParkingHubClient> Clients => ClientsFake;
        public IGroupManager Groups => throw new NotSupportedException();
    }

    private sealed class FakeHubClients : IHubClients<IParkingHubClient>
    {
        public RecordingParkingHubClient GroupClient { get; } = new();

        public IParkingHubClient Group(string groupName)
        {
            GroupClient.CurrentGroup = groupName;
            return GroupClient;
        }

        public IParkingHubClient All => throw new NotSupportedException();
        public IParkingHubClient AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IParkingHubClient Client(string connectionId) => throw new NotSupportedException();
        public IParkingHubClient Clients(IReadOnlyList<string> connectionIds) => throw new NotSupportedException();
        public IParkingHubClient GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IParkingHubClient Groups(IReadOnlyList<string> groupNames) => throw new NotSupportedException();
        public IParkingHubClient User(string userId) => throw new NotSupportedException();
        public IParkingHubClient Users(IReadOnlyList<string> userIds) => throw new NotSupportedException();
    }

    private sealed class RecordingParkingHubClient : IParkingHubClient
    {
        public string CurrentGroup { get; set; } = string.Empty;
        public List<(string GroupName, SlotStatusChangedMessage Message)> SlotStatusMessages { get; } = [];
        public List<(string GroupName, LotOccupancyChangedMessage Message)> OccupancyMessages { get; } = [];
        public List<(string GroupName, SlotNeedsVerificationMessage Message)> VerificationMessages { get; } = [];

        public Task SlotStatusChanged(SlotStatusChangedMessage message)
        {
            SlotStatusMessages.Add((CurrentGroup, message));
            return Task.CompletedTask;
        }

        public Task LotOccupancyChanged(LotOccupancyChangedMessage message)
        {
            OccupancyMessages.Add((CurrentGroup, message));
            return Task.CompletedTask;
        }

        public Task SlotNeedsVerification(SlotNeedsVerificationMessage message)
        {
            VerificationMessages.Add((CurrentGroup, message));
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingGroupManager : IGroupManager
    {
        public List<(string ConnectionId, string GroupName)> Added { get; } = [];
        public List<(string ConnectionId, string GroupName)> Removed { get; } = [];

        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            Added.Add((connectionId, groupName));
            return Task.CompletedTask;
        }

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            Removed.Add((connectionId, groupName));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeHubCallerContext(
        string connectionId,
        ClaimsPrincipal user,
        string? userIdentifier,
        CancellationToken connectionAborted) : HubCallerContext
    {
        public override string ConnectionId => connectionId;
        public override string? UserIdentifier => userIdentifier;
        public override ClaimsPrincipal? User => user;
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted => connectionAborted;
        public override void Abort() { }
    }

    private sealed class TestJwtKeys : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), $"parking-hub-test-key-{Guid.NewGuid():N}");

        public TestJwtKeys()
        {
            Directory.CreateDirectory(directory);
            PrivateKey = RSA.Create(2048);
            PublicKeyPath = Path.Combine(directory, "public.pem");
            File.WriteAllText(PublicKeyPath, PrivateKey.ExportSubjectPublicKeyInfoPem());
        }

        public RSA PrivateKey { get; }
        public string PublicKeyPath { get; }

        public void Dispose()
        {
            PrivateKey.Dispose();
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> messages = new();
        public IReadOnlyCollection<string> Messages => messages.ToArray();
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(messages);
        public void Dispose() { }
    }

    private sealed class CapturingLogger(ConcurrentQueue<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => EmptyScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            messages.Enqueue($"{formatter(state, exception)} {exception}");
    }

    private sealed class EmptyScope : IDisposable
    {
        public static EmptyScope Instance { get; } = new();
        public void Dispose() { }
    }
}
