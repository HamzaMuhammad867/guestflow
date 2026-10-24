using GuestFlow.Api.Data;
using GuestFlow.Api.Models;
using GuestFlow.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace GuestFlow.Api.Endpoints;

public static class Endpoints
{
    public static void MapGuestFlowEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        // ---- Rooms -------------------------------------------------------------
        api.MapGet("/rooms/availability", async (IRoomAvailabilityService svc, HttpResponse res, CancellationToken ct) =>
        {
            var (summary, fromCache) = await svc.GetSummaryAsync(ct);
            res.Headers["X-Cache"] = fromCache ? "HIT" : "MISS";
            return Results.Ok(summary);
        });

        api.MapPut("/rooms/{number}/status", async (string number, RoomStatus status, AppDbContext db,
            IRoomAvailabilityService svc, CancellationToken ct) =>
        {
            var room = await db.Rooms.SingleOrDefaultAsync(r => r.Number == number, ct);
            if (room is null) return Results.NotFound();
            room.Status = status;
            await db.SaveChangesAsync(ct);
            await svc.InvalidateAsync(ct);
            return Results.Ok(room);
        });

        // ---- Guest requests ----------------------------------------------------
        api.MapGet("/requests", async (RequestStatus? status, AppDbContext db, CancellationToken ct) =>
        {
            var q = db.GuestRequests.AsNoTracking();
            if (status is not null) q = q.Where(r => r.Status == status);
            return Results.Ok(await q.OrderByDescending(r => r.CreatedAtUtc).Take(200).ToListAsync(ct));
        });

        api.MapGet("/requests/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
            await db.GuestRequests.FindAsync([id], ct) is { } r ? Results.Ok(r) : Results.NotFound());

        api.MapPost("/requests", async (CreateRequestDto dto, AppDbContext db, ILoggerFactory lf, CancellationToken ct) =>
        {
            var errors = RequestValidator.Validate(dto);
            if (errors.Count > 0) return Results.ValidationProblem(errors);

            if (!await db.Rooms.AnyAsync(r => r.Number == dto.RoomNumber, ct))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                    { ["roomNumber"] = [$"Room {dto.RoomNumber} does not exist."] });

            var entity = new GuestRequest
            {
                RoomNumber = dto.RoomNumber.Trim(),
                Category = dto.Category,
                Description = dto.Description.Trim()
            };
            db.GuestRequests.Add(entity);
            await db.SaveChangesAsync(ct);

            lf.CreateLogger("GuestRequests").LogInformation(
                "Guest request {Id} created for room {Room} ({Category})", entity.Id, entity.RoomNumber, entity.Category);
            GuestFlowMetrics.RequestsCreated.Add(1, new KeyValuePair<string, object?>("category", entity.Category.ToString()));

            return Results.Created($"/api/requests/{entity.Id}", entity);
        });

        api.MapPatch("/requests/{id:int}/status", async (int id, UpdateStatusDto dto, AppDbContext db, CancellationToken ct) =>
        {
            var entity = await db.GuestRequests.FindAsync([id], ct);
            if (entity is null) return Results.NotFound();

            if (!RequestValidator.IsValidTransition(entity.Status, dto.Status))
                return Results.Conflict(new { error = $"Cannot move a request from {entity.Status} to {dto.Status}." });

            entity.Status = dto.Status;
            if (dto.Status == RequestStatus.Done) entity.CompletedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Ok(entity);
        });
    }
}
