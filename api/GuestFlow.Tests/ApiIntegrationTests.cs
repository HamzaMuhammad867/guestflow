using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using GuestFlow.Api.Data;
using GuestFlow.Api.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GuestFlow.Tests;

/// <summary>Boots the real API in memory with an in-memory database and cache (no SQL Server/Redis needed).</summary>
public class GuestFlowApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(_dbName));
            services.AddDistributedMemoryCache();

            using var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Rooms.AddRange(
                new Room { Number = "101", Type = "King", Floor = 1, Status = RoomStatus.Available },
                new Room { Number = "102", Type = "Twin", Floor = 1, Status = RoomStatus.Occupied });
            db.SaveChanges();
        });
    }
}

// Integration tests: real HTTP pipeline, routing, validation and JSON serialisation.
public class ApiIntegrationTests(GuestFlowApiFactory factory) : IClassFixture<GuestFlowApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Health_endpoint_reports_healthy()
    {
        var res = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Create_request_returns_201_and_can_be_fetched()
    {
        var res = await _client.PostAsJsonAsync("/api/requests",
            new CreateRequestDto("101", RequestCategory.Housekeeping, "Extra towels"), Json);

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var created = await res.Content.ReadFromJsonAsync<GuestRequest>(Json);
        Assert.NotNull(created);
        Assert.Equal(RequestStatus.Open, created!.Status);

        var fetched = await _client.GetFromJsonAsync<GuestRequest>($"/api/requests/{created.Id}", Json);
        Assert.Equal("Extra towels", fetched!.Description);
    }

    [Fact]
    public async Task Unknown_room_is_rejected_with_400()
    {
        var res = await _client.PostAsJsonAsync("/api/requests",
            new CreateRequestDto("999", RequestCategory.Maintenance, "AC not cooling"), Json);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Invalid_status_transition_returns_409()
    {
        var res = await _client.PostAsJsonAsync("/api/requests",
            new CreateRequestDto("102", RequestCategory.Dining, "Late breakfast"), Json);
        var created = (await res.Content.ReadFromJsonAsync<GuestRequest>(Json))!;

        var done = await _client.PatchAsJsonAsync($"/api/requests/{created.Id}/status",
            new UpdateStatusDto(RequestStatus.Done), Json);
        Assert.Equal(HttpStatusCode.OK, done.StatusCode);

        var reopen = await _client.PatchAsJsonAsync($"/api/requests/{created.Id}/status",
            new UpdateStatusDto(RequestStatus.Open), Json);
        Assert.Equal(HttpStatusCode.Conflict, reopen.StatusCode);
    }

    [Fact]
    public async Task Availability_is_served_from_cache_on_second_call()
    {
        // Force a known state first, then check MISS → HIT
        await _client.PutAsync("/api/rooms/101/status?status=Available", null);

        var first = await _client.GetAsync("/api/rooms/availability");
        var second = await _client.GetAsync("/api/rooms/availability");

        Assert.Equal("MISS", first.Headers.GetValues("X-Cache").Single());
        Assert.Equal("HIT", second.Headers.GetValues("X-Cache").Single());
    }
}
