using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Memoressa.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Memoressa.Api.Tests;

public class ActivityAlbumUpdateApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ActivityAlbumUpdateApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<MemoressaDbContext>));
                if (descriptor is not null)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<MemoressaDbContext>(options =>
                    options.UseInMemoryDatabase("MemoressaActivityUpdateTests"));
            });
        });
    }

    [Fact]
    public async Task Put_Activity_With_Empty_Agenda_Does_Not_Throw()
    {
        var client = _factory.CreateClient();
        await RegisterAndAuthAsync(client);

        var createResponse = await client.PostAsJsonAsync("/api/v1/activities", new
        {
            title = "Trip",
            activityType = "travel",
            status = "inProgress",
            startDate = "2026-09-28",
            endDate = "2026-09-30",
            location = "Tokyo",
            privacyScope = "family",
            agenda = new[]
            {
                new
                {
                    id = "day1",
                    title = "Day 1",
                    startDate = "2026-09-28"
                }
            }
        });
        Assert.True(
            createResponse.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created,
            await createResponse.Content.ReadAsStringAsync());
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var activityId = created.GetProperty("data").GetProperty("id").GetString();
        Assert.False(string.IsNullOrWhiteSpace(activityId));

        var updateBody = new
        {
            title = "Trip updated",
            activityType = "travel",
            status = "inProgress",
            startDate = "2026-09-28",
            endDate = "2026-09-30",
            location = "Tokyo",
            privacyScope = "family",
            familyMemberIds = Array.Empty<Guid>(),
            friendIds = Array.Empty<string>(),
            agenda = Array.Empty<object>()
        };

        var putResponse = await client.PutAsJsonAsync($"/api/v1/activities/{activityId}", updateBody);
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);

        var putAgain = await client.PutAsJsonAsync($"/api/v1/activities/{activityId}", updateBody);
        Assert.Equal(HttpStatusCode.OK, putAgain.StatusCode);

        var dto = await putAgain.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var activity = dto.GetProperty("data");
        Assert.Equal("Trip updated", activity.GetProperty("title").GetString());
        Assert.Equal(0, activity.GetProperty("agenda").GetArrayLength());
    }

    private static async Task RegisterAndAuthAsync(HttpClient client)
    {
        var registerResponse = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = $"activity-{Guid.NewGuid():N}@memoressa.com",
            password = "Password123!",
            nickname = "Tester"
        });
        registerResponse.EnsureSuccessStatusCode();
        var registerBody = await registerResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var accessToken = registerBody.GetProperty("tokens").GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }
}
