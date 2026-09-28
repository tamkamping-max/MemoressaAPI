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

public class PhotoUserTagLibraryApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public PhotoUserTagLibraryApiTests(WebApplicationFactory<Program> factory)
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
                    options.UseInMemoryDatabase("MemoressaPhotoTagLibraryTests"));
            });
        });
    }

    [Fact]
    public async Task Library_Create_List_Delete_DoesNotRequirePhoto()
    {
        var client = _factory.CreateClient();
        var email = $"tags-{Guid.NewGuid():N}@memoressa.com";

        var registerResponse = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password = "Password123!",
            nickname = "TagTester"
        });
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        var registerBody = await registerResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var accessToken = registerBody.GetProperty("tokens").GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/photo-tags/library",
            new { tag = "  reunion  " });
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var entryId = created.GetProperty("id").GetGuid();
        Assert.Equal("reunion", created.GetProperty("tag").GetString());

        var duplicateResponse = await client.PostAsJsonAsync(
            "/api/v1/photo-tags/library",
            new { tag = "Reunion" });
        Assert.Equal(HttpStatusCode.OK, duplicateResponse.StatusCode);
        var duplicate = await duplicateResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(entryId, duplicate.GetProperty("id").GetGuid());

        var listResponse = await client.GetAsync("/api/v1/photo-tags/library");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = await listResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(1, list.GetArrayLength());

        var deleteResponse = await client.DeleteAsync($"/api/v1/photo-tags/library/{entryId}");
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        var listAfterDelete = await client.GetAsync("/api/v1/photo-tags/library");
        var emptyList = await listAfterDelete.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(0, emptyList.GetArrayLength());
    }
}
