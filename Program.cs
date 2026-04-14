using RealTime.Hubs;
using RealTime.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<RedisConnectionService>();
builder.Services.AddSingleton<IRedisConnectionService>(sp => sp.GetRequiredService<RedisConnectionService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<RedisConnectionService>());
builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
              .SetIsOriginAllowed(_ => true);
    });
});

var app = builder.Build();

app.UseCors();

app.MapGet("/health", () => "OK");
app.MapHub<RealtimeHub>("/realtime");

app.Run();