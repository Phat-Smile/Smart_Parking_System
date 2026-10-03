using ParkingSystem.AspNetCore;
using ParkingSystem.AspNetCore.Security;

var builder = WebApplication.CreateBuilder(args);
builder.AddParkingSystemWebFoundation();
builder.AddParkingSystemGatewaySecurity();
builder.AddParkingSystemTelemetry("parking-gateway");
builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();

var app = builder.Build();
app.UseParkingSystemWebFoundation();
app.UseParkingSystemGatewaySecurity();
if (app.Environment.IsDevelopment())
    app.MapOpenApi();
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");
app.MapReverseProxy();
app.Run();
