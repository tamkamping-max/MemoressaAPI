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

public class AuthSelfFamilyMemberTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public AuthSelfFamilyMemberTests(WebApplicationFactory<Program> factory)
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
                    options.UseInMemoryDatabase("MemoressaSelfMemberTests"));
            });
        });
    }

    [Fact]
    public async Task PatchMe_SetsAndClearsSelfFamilyMemberId()
    {
        var client = _factory.CreateClient();
        var registerResponse = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = $"self-{Guid.NewGuid():N}@memoressa.com",
            password = "Password123!",
            nickname = "Self"
        });
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        var registerBody = await registerResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(registerBody.GetProperty("user").TryGetProperty("selfFamilyMemberId", out var regSelf));
        Assert.Equal(JsonValueKind.Null, regSelf.ValueKind);

        var accessToken = registerBody.GetProperty("tokens").GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var memberResponse = await client.PostAsJsonAsync("/api/v1/family-members", new
        {
            name = "Me",
            generation = "self"
        });
        if (memberResponse.StatusCode != HttpStatusCode.OK)
        {
            var error = await memberResponse.Content.ReadAsStringAsync();
            Assert.Fail($"Create member failed: {(int)memberResponse.StatusCode} {error}");
        }
        var memberBody = await memberResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var memberId = memberBody.GetProperty("id").GetGuid();

        var patchResponse = await client.PatchAsJsonAsync("/api/v1/auth/me", new { selfFamilyMemberId = memberId });
        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);
        var patched = await patchResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(memberId, patched.GetProperty("selfFamilyMemberId").GetGuid());

        var clearResponse = await client.PatchAsJsonAsync("/api/v1/auth/me", new { selfFamilyMemberId = (Guid?)null });
        Assert.Equal(HttpStatusCode.OK, clearResponse.StatusCode);
        var cleared = await clearResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(JsonValueKind.Null, cleared.GetProperty("selfFamilyMemberId").ValueKind);
    }

    [Fact]
    public async Task PatchMe_RejectsMemberFromAnotherFamily()
    {
        var clientA = _factory.CreateClient();
        await RegisterAndAuthAsync(clientA, "a");
        var memberA = await CreateMemberAsync(clientA, "Member A");

        var clientB = _factory.CreateClient();
        await RegisterAndAuthAsync(clientB, "b");

        var patchResponse = await clientB.PatchAsJsonAsync("/api/v1/auth/me", new { selfFamilyMemberId = memberA });
        Assert.Equal(HttpStatusCode.NotFound, patchResponse.StatusCode);
    }

    private static async Task RegisterAndAuthAsync(HttpClient client, string suffix)
    {
        var registerResponse = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = $"user-{suffix}-{Guid.NewGuid():N}@memoressa.com",
            password = "Password123!",
            nickname = suffix
        });
        registerResponse.EnsureSuccessStatusCode();
        var body = await registerResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var token = body.GetProperty("tokens").GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static async Task<Guid> CreateMemberAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/v1/family-members", new { name, generation = "self" });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return body.GetProperty("id").GetGuid();
    }
}
