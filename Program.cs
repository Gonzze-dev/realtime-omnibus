using Microsoft.EntityFrameworkCore;
using RealTime.Data;
using RealTime.Hubs;
using RealTime.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PostgreSQL")));
builder.Services.AddSingleton<PgNotificationService>();
builder.Services.AddSignalR();
// CORS_ALLOWED_ORIGINS: orígenes separados por coma. Vacío = se permiten todos.
var corsAllowedOrigins = (builder.Configuration["CORS_ALLOWED_ORIGINS"] ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();

        if (corsAllowedOrigins.Length == 0)
            policy.SetIsOriginAllowed(_ => true);
        else
            policy.WithOrigins(corsAllowedOrigins);
    });
});

var app = builder.Build();

app.UseCors();

app.MapGet("/", () => Results.Redirect("/health"));
app.MapGet("/health", () => "OK");
app.MapHub<RealtimeHub>("/realtime");

app.Run();
