using GuestFlow.Api.Models;
using GuestFlow.Api.Services;

namespace GuestFlow.Tests;

// Unit tests: pure business rules, no database or HTTP.
public class RequestValidatorTests
{
    [Fact]
    public void Valid_request_has_no_errors()
    {
        var errors = RequestValidator.Validate(new CreateRequestDto("101", RequestCategory.Housekeeping, "Extra towels"));
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("", "Extra towels", "roomNumber")]
    [InlineData("101", "", "description")]
    [InlineData("101", "   ", "description")]
    public void Missing_fields_are_rejected(string room, string description, string expectedField)
    {
        var errors = RequestValidator.Validate(new CreateRequestDto(room, RequestCategory.Dining, description));
        Assert.Contains(expectedField, errors.Keys);
    }

    [Fact]
    public void Description_over_limit_is_rejected()
    {
        var longText = new string('x', RequestValidator.MaxDescriptionLength + 1);
        var errors = RequestValidator.Validate(new CreateRequestDto("101", RequestCategory.Concierge, longText));
        Assert.Contains("description", errors.Keys);
    }

    [Fact]
    public void Unknown_category_is_rejected()
    {
        var errors = RequestValidator.Validate(new CreateRequestDto("101", (RequestCategory)99, "Test"));
        Assert.Contains("category", errors.Keys);
    }

    [Theory]
    [InlineData(RequestStatus.Open, RequestStatus.InProgress, true)]
    [InlineData(RequestStatus.Open, RequestStatus.Done, true)]
    [InlineData(RequestStatus.InProgress, RequestStatus.Done, true)]
    [InlineData(RequestStatus.Done, RequestStatus.Open, false)]
    [InlineData(RequestStatus.Done, RequestStatus.InProgress, false)]
    [InlineData(RequestStatus.InProgress, RequestStatus.Open, false)]
    public void Status_workflow_is_enforced(RequestStatus from, RequestStatus to, bool expected)
        => Assert.Equal(expected, RequestValidator.IsValidTransition(from, to));
}
