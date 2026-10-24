using GuestFlow.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GuestFlow.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<GuestRequest> GuestRequests => Set<GuestRequest>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Room>(e =>
        {
            e.HasIndex(r => r.Number).IsUnique();
            e.Property(r => r.Number).HasMaxLength(10);
            e.Property(r => r.Type).HasMaxLength(30);
            e.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        });

        b.Entity<GuestRequest>(e =>
        {
            e.Property(r => r.RoomNumber).HasMaxLength(10);
            e.Property(r => r.Description).HasMaxLength(500);
            e.Property(r => r.Category).HasConversion<string>().HasMaxLength(20);
            e.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(r => r.Status);
        });
    }
}

public static class SeedData
{
    /// <summary>Creates the schema and 40 sample rooms across 4 floors if the database is empty.</summary>
    public static async Task EnsureSeededAsync(AppDbContext db, ILogger logger, CancellationToken ct = default)
    {
        // SQL Server in Docker can take a few seconds to accept connections – retry instead of crashing.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.Database.EnsureCreatedAsync(ct);
                break;
            }
            catch (Exception ex) when (attempt < 10)
            {
                logger.LogWarning(ex, "Database not ready (attempt {Attempt}/10), retrying in 3s", attempt);
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
            }
        }

        if (await db.Rooms.AnyAsync(ct)) return;

        var types = new[] { "King", "Twin", "Sea View King", "Suite" };
        var rooms = new List<Room>();
        for (var floor = 1; floor <= 4; floor++)
        for (var n = 1; n <= 10; n++)
        {
            rooms.Add(new Room
            {
                Number = $"{floor}{n:00}",
                Floor = floor,
                Type = types[(n - 1) % types.Length],
                Status = (n % 4) switch
                {
                    0 => RoomStatus.Occupied,
                    1 => RoomStatus.Available,
                    2 => RoomStatus.Available,
                    _ => RoomStatus.Cleaning
                }
            });
        }
        db.Rooms.AddRange(rooms);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded {Count} rooms", rooms.Count);
    }
}
