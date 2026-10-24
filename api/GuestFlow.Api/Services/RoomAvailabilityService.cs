using System.Text.Json;
using GuestFlow.Api.Data;
using GuestFlow.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace GuestFlow.Api.Services;

public interface IRoomAvailabilityService
{
    Task<(AvailabilitySummary Summary, bool FromCache)> GetSummaryAsync(CancellationToken ct = default);
    Task InvalidateAsync(CancellationToken ct = default);
}

/// <summary>
/// Cache-aside pattern: read the availability summary from Redis; on a miss, compute it from SQL Server
/// and store it for a short time. Front-desk screens poll this endpoint often, so the cache keeps load
/// off the database. The cache is invalidated whenever room data changes.
/// </summary>
public class RoomAvailabilityService(AppDbContext db, IDistributedCache cache, ILogger<RoomAvailabilityService> logger)
    : IRoomAvailabilityService
{
    internal const string CacheKey = "rooms:availability:v1";
    internal static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);

    public async Task<(AvailabilitySummary Summary, bool FromCache)> GetSummaryAsync(CancellationToken ct = default)
    {
        try
        {
            var cached = await cache.GetStringAsync(CacheKey, ct);
            if (cached is not null)
            {
                var hit = JsonSerializer.Deserialize<AvailabilitySummary>(cached);
                if (hit is not null) return (hit, true);
            }
        }
        catch (Exception ex)
        {
            // Redis being down must never take the API down – fall back to the database.
            logger.LogWarning(ex, "Cache read failed; serving from database");
        }

        var summary = await BuildSummaryAsync(ct);

        try
        {
            await cache.SetStringAsync(CacheKey, JsonSerializer.Serialize(summary),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl }, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache write failed");
        }

        return (summary, false);
    }

    public async Task InvalidateAsync(CancellationToken ct = default)
    {
        try { await cache.RemoveAsync(CacheKey, ct); }
        catch (Exception ex) { logger.LogWarning(ex, "Cache invalidation failed"); }
    }

    internal async Task<AvailabilitySummary> BuildSummaryAsync(CancellationToken ct)
    {
        var rooms = await db.Rooms.AsNoTracking().ToListAsync(ct);
        return Summarise(rooms, DateTime.UtcNow);
    }

    /// <summary>Pure function – easy to unit test.</summary>
    internal static AvailabilitySummary Summarise(IReadOnlyCollection<Room> rooms, DateTime now) => new(
        Total: rooms.Count,
        Available: rooms.Count(r => r.Status == RoomStatus.Available),
        Occupied: rooms.Count(r => r.Status == RoomStatus.Occupied),
        Cleaning: rooms.Count(r => r.Status == RoomStatus.Cleaning),
        OutOfService: rooms.Count(r => r.Status == RoomStatus.OutOfService),
        AvailableRooms: rooms.Where(r => r.Status == RoomStatus.Available)
                             .Select(r => r.Number).OrderBy(n => n).ToList(),
        GeneratedAtUtc: now);
}
