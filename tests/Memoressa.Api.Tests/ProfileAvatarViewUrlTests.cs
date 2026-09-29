using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;
using Memoressa.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Memoressa.Api.Tests;

public class ProfileAvatarViewUrlTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ProfileAvatarViewUrlTests(WebApplicationFactory<Program> factory)
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
                    options.UseInMemoryDatabase("MemoressaProfileAvatarViewUrlTests"));
            });
        });
    }

    [Fact]
    public async Task GetFriends_IncludesFriendUserWithAvatarUrl()
    {
        var ownerClient = _factory.CreateClient();
        var ownerId = await RegisterAndAuthAsync(ownerClient, "owner");

        var friendClient = _factory.CreateClient();
        var friendUserId = await RegisterAndAuthAsync(friendClient, "friend");

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MemoressaDbContext>();
            var friendAccount = await db.UserAccounts.FirstAsync(u => u.Id == friendUserId);
            friendAccount.AvatarUrl = "avatars/users/friend/key.jpg";
            await db.SaveChangesAsync();
        }

        var inviteResponse = await ownerClient.PostAsJsonAsync("/api/v1/friends/invites", new
        {
            email = await GetEmailForUserAsync(friendUserId)
        });
        inviteResponse.EnsureSuccessStatusCode();

        var accept = await friendClient.PostAsync(
            $"/api/v1/friends/invites/{(await inviteResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid()}/accept",
            null);
        accept.EnsureSuccessStatusCode();

        var friendsResponse = await ownerClient.GetAsync("/api/v1/friends");
        friendsResponse.EnsureSuccessStatusCode();
        var friends = await friendsResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var accepted = friends.EnumerateArray().First(f => f.GetProperty("status").GetString() == "accepted");
        Assert.True(accepted.TryGetProperty("friendUser", out var friendUser));
        Assert.Equal(friendUserId, friendUser.GetProperty("id").GetGuid());
        Assert.True(accepted.TryGetProperty("avatarUrl", out _));
        Assert.True(friendUser.TryGetProperty("avatarUrl", out _));
    }

    [Fact]
    public async Task ViewUrl_ByFriendUserId_ReturnsKeyAndViewUrl()
    {
        var ownerClient = _factory.CreateClient();
        await RegisterAndAuthAsync(ownerClient, "viewer");

        var friendClient = _factory.CreateClient();
        var friendUserId = await RegisterAndAuthAsync(friendClient, "pal");

        const string avatarKey = "avatars/users/pal/abc.jpg";
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MemoressaDbContext>();
            var account = await db.UserAccounts.FirstAsync(u => u.Id == friendUserId);
            account.AvatarUrl = avatarKey;
            await db.SaveChangesAsync();
        }

        var invite = await ownerClient.PostAsJsonAsync("/api/v1/friends/invites", new
        {
            email = await GetEmailForUserAsync(friendUserId)
        });
        invite.EnsureSuccessStatusCode();
        var inviteId = (await invite.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();
        await friendClient.PostAsync($"/api/v1/friends/invites/{inviteId}/accept", null);

        var viewResponse = await ownerClient.GetAsync(
            $"/api/v1/profile/avatars/view-url?friendUserId={friendUserId}");
        Assert.Equal(HttpStatusCode.OK, viewResponse.StatusCode);
        var body = await viewResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(avatarKey, body.GetProperty("avatarUrl").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("viewUrl").GetString()));
    }

    private async Task<string> GetEmailForUserAsync(Guid userId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MemoressaDbContext>();
        return (await db.UserAccounts.AsNoTracking().FirstAsync(u => u.Id == userId)).Email;
    }

    private static async Task<Guid> RegisterAndAuthAsync(HttpClient client, string suffix)
    {
        var registerResponse = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = $"viewurl-{suffix}-{Guid.NewGuid():N}@memoressa.com",
            password = "Password123!",
            nickname = suffix
        });
        registerResponse.EnsureSuccessStatusCode();
        var registerBody = await registerResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var accessToken = registerBody.GetProperty("tokens").GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return registerBody.GetProperty("user").GetProperty("id").GetGuid();
    }
}
