using GuestFlow.Api.Data;
using GuestFlow.Api.Models;
using GuestFlow.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GuestFlow.Tests;

// Unit tests for the cache-aside logic, using an in-memory database and in-memory cache.
public class RoomAvailabilityServiceTests
{
    private static (RoomAvailabilityService Svc, AppDbContext Db) Create()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Rooms.AddRange(
            new Room { Number = "101", Status = RoomStatus.Available },
            new Room { Number = "102", Status = RoomStatus.Occupied },
            new Room { Number = "103", Status = RoomStatus.Cleaning },
            new Room { Number = "104", Status = RoomStatus.Available });
        db.SaveChanges();

        IDistributedCache cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        return (new RoomAvailabilityService(db, cache, NullLogger<RoomAvailabilityService>.Instance), db);
    }

    [Fact]
    public void Summarise_counts_each_status()
    {
        var rooms = new[]
        {
            new Room { Number = "201", Status = RoomStatus.Available },
            new Room { Number = "202", Status = RoomStatus.OutOfService },
            new Room { Number = "200", Status = RoomStatus.Available },
        };

        var s = RoomAvailabilityService.Summarise(rooms, DateTime.UtcNow);

        Assert.Equal(3, s.Total);
        Assert.Equal(2, s.Available);
        Assert.Equal(1, s.OutOfService);
        Assert.Equal(new[] { "200", "201" }, s.AvailableRooms); // sorted
    }

    [Fact]
    public async Task First_call_is_a_cache_miss_second_is_a_hit()
    {
        var (svc, _) = Create();

        var first = await svc.GetSummaryAsync();
        var second = await svc.GetSummaryAsync();

        Assert.False(first.FromCache);
        Assert.True(second.FromCache);
        Assert.Equal(2, second.Summary.Available);
    }

    [Fact]
    public async Task Invalidate_forces_fresh_data_from_database()
    {
        var (svc, db) = Create();
        await svc.GetSummaryAsync(); // warm cache

        var room = await db.Rooms.SingleAsync(r => r.Number == "102");
        room.Status = RoomStatus.Available;
        await db.SaveChangesAsync();
        await svc.InvalidateAsync();

        var after = await svc.GetSummaryAsync();
        Assert.False(after.FromCache);
        Assert.Equal(3, after.Summary.Available);
    }
}
