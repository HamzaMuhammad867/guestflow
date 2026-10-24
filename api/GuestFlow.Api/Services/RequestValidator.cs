using GuestFlow.Api.Models;

namespace GuestFlow.Api.Services;

public static class RequestValidator
{
    public const int MaxDescriptionLength = 500;

    /// <summary>Returns a dictionary of field errors (empty when valid), in the shape ASP.NET's ValidationProblem expects.</summary>
    public static Dictionary<string, string[]> Validate(CreateRequestDto dto)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(dto.RoomNumber))
            errors["roomNumber"] = ["Room number is required."];

        if (!Enum.IsDefined(dto.Category))
            errors["category"] = ["Unknown category."];

        if (string.IsNullOrWhiteSpace(dto.Description))
            errors["description"] = ["Description is required."];
        else if (dto.Description.Length > MaxDescriptionLength)
            errors["description"] = [$"Description must be {MaxDescriptionLength} characters or fewer."];

        return errors;
    }

    /// <summary>Allowed workflow: Open → InProgress → Done (Open → Done allowed for quick fixes). Done is final.</summary>
    public static bool IsValidTransition(RequestStatus from, RequestStatus to) => (from, to) switch
    {
        (RequestStatus.Open, RequestStatus.InProgress) => true,
        (RequestStatus.Open, RequestStatus.Done) => true,
        (RequestStatus.InProgress, RequestStatus.Done) => true,
        _ => false
    };
}
