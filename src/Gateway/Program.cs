using ParkingSystem.AspNetCore;
using ParkingSystem.AspNetCore.Security;
using ParkingSystem.Gateway.Security;
using Yarp.ReverseProxy.Transforms.Builder;

var builder = WebApplication.CreateBuilder(args);
builder.AddParkingSystemWebFoundation();
builder.AddParkingSystemGatewaySecurity();
builder.AddParkingSystemAuthRateLimiting();
builder.AddParkingSystemSearchRateLimiting();
builder.AddParkingSystemTelemetry("parking-gateway");
builder.Services.AddSingleton<ITransformProvider, GatewayServiceKeyTransform>();
builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();

var app = builder.Build();
app.UseParkingSystemWebFoundation();
app.UseParkingSystemGatewaySecurity();
// Sau auth, trước proxy: request Auth vượt ngưỡng bị chặn ngay tại Gateway, không tới Identity.
app.UseRateLimiter();
if (app.Environment.IsDevelopment())
    app.MapOpenApi();
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");
app.MapReverseProxy();
app.Run();
