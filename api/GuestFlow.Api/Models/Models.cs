namespace GuestFlow.Api.Models;

public enum RoomStatus { Available, Occupied, Cleaning, OutOfService }

public enum RequestCategory { Housekeeping, Maintenance, Dining, Concierge }

public enum RequestStatus { Open, InProgress, Done }

public class Room
{
    public int Id { get; set; }
    public string Number { get; set; } = "";
    public string Type { get; set; } = "";      // e.g. King, Twin, Suite
    public int Floor { get; set; }
    public RoomStatus Status { get; set; } = RoomStatus.Available;
}

public class GuestRequest
{
    public int Id { get; set; }
    public string RoomNumber { get; set; } = "";
    public RequestCategory Category { get; set; }
    public string Description { get; set; } = "";
    public RequestStatus Status { get; set; } = RequestStatus.Open;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
}

// API contracts (DTOs) – keep the database shape separate from what clients send
public record CreateRequestDto(string RoomNumber, RequestCategory Category, string Description);

public record UpdateStatusDto(RequestStatus Status);

public record AvailabilitySummary(int Total, int Available, int Occupied, int Cleaning, int OutOfService,
    IReadOnlyList<string> AvailableRooms, DateTime GeneratedAtUtc);
