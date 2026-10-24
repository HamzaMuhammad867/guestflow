using System.Diagnostics.Metrics;
using System.Text.Json.Serialization;
using GuestFlow.Api.Data;
using GuestFlow.Api.Endpoints;
using GuestFlow.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---- Configuration (overridden by environment variables in Docker / Azure) ----
var sqlConn = builder.Configuration.GetConnectionString("Sql");
var redisConn = builder.Configuration.GetConnectionString("Redis");

// ---- Services --------------------------------------------------------------------
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(sqlConn,
        sql => sql.EnableRetryOnFailure(maxRetryCount: 5)));

    if (!string.IsNullOrWhiteSpace(redisConn))
        builder.Services.AddStackExchangeRedisCache(o => { o.Configuration = redisConn; o.InstanceName = "guestflow:"; });
    else
        builder.Services.AddDistributedMemoryCache(); // local fallback when Redis isn't configured
}

builder.Services.AddScoped<IRoomAvailabilityService, RoomAvailabilityService>();
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("sql");
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(builder.Configuration["Cors:AllowedOrigin"] ?? "http://localhost:5173")
     .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// ---- Middleware -------------------------------------------------------------------
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();

app.MapHealthChecks("/health");
app.MapGuestFlowEndpoints();

// Create schema + seed sample rooms (EnsureCreated keeps the demo simple; a production system would use EF migrations)
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await SeedData.EnsureSeededAsync(db, app.Logger);
}

app.Run();

/// <summary>Custom metrics – visible with `dotnet-counters monitor -n GuestFlow.Api --counters GuestFlow`
/// and exportable to Azure Monitor / DataDog through OpenTelemetry.</summary>
public static class GuestFlowMetrics
{
    private static readonly Meter Meter = new("GuestFlow", "1.0");
    public static readonly Counter<long> RequestsCreated = Meter.CreateCounter<long>("guestflow.requests.created");
}

public partial class Program { } // makes Program visible to WebApplicationFactory in integration tests
