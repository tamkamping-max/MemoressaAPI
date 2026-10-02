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

public class ActivityDeleteApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ActivityDeleteApiTests(WebApplicationFactory<Program> factory)
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
                    options.UseInMemoryDatabase("MemoressaActivityDeleteTests"));
            });
        });
    }

    [Fact]
    public async Task Delete_RemovesActivityFromInProgress_AndLeavesPhotos()
    {
        var client = _factory.CreateClient();
        await RegisterAndAuthAsync(client, "creator");

        var createResponse = await client.PostAsJsonAsync("/api/v1/activities", new
        {
            title = "To delete",
            activityType = "travel",
            status = "inProgress",
            startDate = "2026-09-28"
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var activityId = created.GetProperty("data").GetProperty("id").GetString();
        Assert.False(string.IsNullOrWhiteSpace(activityId));

        var deleteResponse = await client.DeleteAsync($"/api/v1/activities/{activityId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var inProgress = await client.GetAsync("/api/v1/activities/in-progress");
        inProgress.EnsureSuccessStatusCode();
        var list = await inProgress.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var items = list.GetProperty("data").GetProperty("items");
        Assert.Equal(0, items.GetArrayLength());
    }

    [Fact]
    public async Task Delete_Returns404ForUnknownId()
    {
        var client = _factory.CreateClient();
        await RegisterAndAuthAsync(client, "solo");

        var deleteResponse = await client.DeleteAsync("/api/v1/activities/act_doesnotexist000");
        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
    }

    private static async Task RegisterAndAuthAsync(HttpClient client, string suffix)
    {
        var registerResponse = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = $"activity-del-{suffix}-{Guid.NewGuid():N}@memoressa.com",
            password = "Password123!",
            nickname = suffix
        });
        registerResponse.EnsureSuccessStatusCode();
        var registerBody = await registerResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var accessToken = registerBody.GetProperty("tokens").GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }
}
