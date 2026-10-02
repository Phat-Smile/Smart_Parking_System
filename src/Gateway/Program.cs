var builder = WebApplication.CreateBuilder(args);
builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();

var app = builder.Build();
if (app.Environment.IsDevelopment())
    app.MapOpenApi();
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");
app.MapReverseProxy();
app.Run();
