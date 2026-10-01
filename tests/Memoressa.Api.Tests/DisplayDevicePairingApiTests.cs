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

public class DisplayDevicePairingApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public DisplayDevicePairingApiTests(WebApplicationFactory<Program> factory)
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
                    options.UseInMemoryDatabase("MemoressaDisplayPairingTests"));
            });
        });
    }

    [Fact]
    public async Task PairingStatus_WithoutSession_ReturnsCanBindFalse()
    {
        var client = _factory.CreateClient();
        var qrCode = Guid.NewGuid();

        var response = await client.GetAsync(
            $"/api/v1/display-devices/pairing-status?qrCode={qrCode}");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.False(body.GetProperty("isBound").GetBoolean());
        Assert.False(body.GetProperty("canBind").GetBoolean());
    }

    [Fact]
    public async Task RegisterPairingSession_ThenStatusShowsCanBind()
    {
        var client = _factory.CreateClient();
        var qrCode = Guid.NewGuid();

        var register = await client.PostAsJsonAsync(
            "/api/v1/display-devices/pairing/register",
            new { qrCode = qrCode.ToString() });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        var status = await client.GetAsync(
            $"/api/v1/display-devices/pairing-status?qrCode={qrCode}");
        status.EnsureSuccessStatusCode();
        var body = await status.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.False(body.GetProperty("isBound").GetBoolean());
        Assert.True(body.GetProperty("canBind").GetBoolean());
    }

    [Fact]
    public async Task Bind_WithoutActiveSession_Returns400()
    {
        var client = _factory.CreateClient();
        await RegisterAndAuthAsync(client);
        var qrCode = Guid.NewGuid();

        var bind = await client.PostAsJsonAsync("/api/v1/display-devices/bind", new
        {
            qrCode = qrCode.ToString(),
            name = "Living room"
        });

        Assert.Equal(HttpStatusCode.BadRequest, bind.StatusCode);
        var err = await bind.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Contains("active Memoressa frame pairing", err.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Bind_AfterRegister_Succeeds()
    {
        var client = _factory.CreateClient();
        await RegisterAndAuthAsync(client);
        var qrCode = Guid.NewGuid();

        await client.PostAsJsonAsync(
            "/api/v1/display-devices/pairing/register",
            new { qrCode = qrCode.ToString() });

        var bind = await client.PostAsJsonAsync("/api/v1/display-devices/bind", new
        {
            qrCode = qrCode.ToString(),
            name = "Kitchen frame"
        });
        bind.EnsureSuccessStatusCode();

        var device = await bind.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(qrCode.ToString(), device.GetProperty("qrCode").GetString());

        var status = await client.GetAsync(
            $"/api/v1/display-devices/pairing-status?qrCode={qrCode}");
        status.EnsureSuccessStatusCode();
        var pairing = await status.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(pairing.GetProperty("isBound").GetBoolean());
        Assert.False(pairing.GetProperty("canBind").GetBoolean());
    }

    [Fact]
    public async Task PairingStatus_InvalidGuid_Returns400()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync(
            "/api/v1/display-devices/pairing-status?qrCode=not-a-guid");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task RegisterAndAuthAsync(HttpClient client)
    {
        var registerResponse = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = $"display-pair-{Guid.NewGuid():N}@memoressa.com",
            password = "Password123!",
            nickname = "FrameOwner"
        });
        registerResponse.EnsureSuccessStatusCode();
        var registerBody = await registerResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var accessToken = registerBody.GetProperty("tokens").GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(accessToken));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var me = await client.GetAsync("/api/v1/auth/me");
        me.EnsureSuccessStatusCode();
    }
}
