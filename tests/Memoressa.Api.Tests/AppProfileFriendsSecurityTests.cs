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

public class AppProfileFriendsSecurityTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public AppProfileFriendsSecurityTests(WebApplicationFactory<Program> factory)
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
                    options.UseInMemoryDatabase("MemoressaAppProfileTests"));
            });
        });
    }

    [Fact]
    public async Task PatchMe_UpdatesProfileFieldsWithoutClearingSelfMember()
    {
        var client = _factory.CreateClient();
        await RegisterAndAuthAsync(client, "profile");

        var memberId = await CreateMemberAsync(client, "Me");
        var setSelf = await client.PatchAsJsonAsync("/api/v1/auth/me", new { selfFamilyMemberId = memberId });
        setSelf.EnsureSuccessStatusCode();

        var patch = await client.PatchAsJsonAsync("/api/v1/auth/me", new
        {
            nickname = "NewNick",
            avatarUrl = "avatars/users/x.jpg",
            birthDate = "1990-01-15T00:00:00Z",
            profileCityId = "city-1"
        });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        var body = await patch.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("NewNick", body.GetProperty("nickname").GetString());
        Assert.Equal(memberId, body.GetProperty("selfFamilyMemberId").GetGuid());
        Assert.Equal("city-1", body.GetProperty("profileCityId").GetString());
    }

    [Fact]
    public async Task FriendInvite_AcceptCreatesFriendsForBothUsers()
    {
        var clientA = _factory.CreateClient();
        await RegisterAndAuthAsync(clientA, "inviter");
        var emailB = $"invitee-{Guid.NewGuid():N}@memoressa.com";

        var clientB = _factory.CreateClient();
        var registerB = await clientB.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = emailB,
            password = "Password123!",
            nickname = "Invitee"
        });
        registerB.EnsureSuccessStatusCode();
        var regBody = await registerB.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var tokenB = regBody.GetProperty("tokens").GetProperty("accessToken").GetString();
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);

        var inviteResponse = await clientA.PostAsJsonAsync("/api/v1/friends/invites", new { email = emailB });
        Assert.Equal(HttpStatusCode.Created, inviteResponse.StatusCode);
        var invite = await inviteResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var inviteId = invite.GetProperty("id").GetGuid();

        var accept = await clientB.PostAsync($"/api/v1/friends/invites/{inviteId}/accept", null);
        Assert.Equal(HttpStatusCode.NoContent, accept.StatusCode);

        var friendsA = await clientA.GetAsync("/api/v1/friends");
        friendsA.EnsureSuccessStatusCode();
        var listA = await friendsA.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(1, listA.GetArrayLength());
        Assert.Equal("accepted", listA[0].GetProperty("status").GetString());

        var friendsB = await clientB.GetAsync("/api/v1/friends");
        friendsB.EnsureSuccessStatusCode();
        var listB = await friendsB.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(1, listB.GetArrayLength());
    }

    [Fact]
    public async Task GetFriends_IncludesPendingOutgoingInvite()
    {
        var client = _factory.CreateClient();
        await RegisterAndAuthAsync(client, "inviter");

        var pendingEmail = $"pending-{Guid.NewGuid():N}@memoressa.com";
        var registerPending = await _factory.CreateClient().PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = pendingEmail,
            password = "Password123!",
            nickname = "Pending"
        });
        registerPending.EnsureSuccessStatusCode();

        var inviteResponse = await client.PostAsJsonAsync("/api/v1/friends/invites", new { email = pendingEmail });
        Assert.Equal(HttpStatusCode.Created, inviteResponse.StatusCode);

        var friendsResponse = await client.GetAsync("/api/v1/friends");
        friendsResponse.EnsureSuccessStatusCode();
        var friends = await friendsResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(1, friends.GetArrayLength());
        Assert.Equal("pending_outgoing", friends[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task PasswordVerify_Returns204Or401()
    {
        var client = _factory.CreateClient();
        await RegisterAndAuthAsync(client, "pwd");

        var ok = await client.PostAsJsonAsync("/api/v1/auth/password/verify", new { currentPassword = "Password123!" });
        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);

        var bad = await client.PostAsJsonAsync("/api/v1/auth/password/verify", new { currentPassword = "wrong" });
        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
    }

    [Fact]
    public async Task AvatarUploadStart_RejectsNonJpegContentType()
    {
        var client = _factory.CreateClient();
        await RegisterAndAuthAsync(client, "avatar");

        var response = await client.PostAsJsonAsync("/api/v1/profile/avatars/upload-start", new
        {
            contentType = "image/png",
            purpose = "user_profile"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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
