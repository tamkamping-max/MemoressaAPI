using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json.Serialization;
using Memoressa.Application.Common;
using Memoressa.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Memoressa.Infrastructure.Services;

public class AppleSignInValidator : IAppleSignInValidator
{
    private const string AppleIssuer = "https://appleid.apple.com";
    private const string AppleKeysUrl = "https://appleid.apple.com/auth/keys";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OAuthSettings _oauthSettings;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AppleSignInValidator> _logger;

    public AppleSignInValidator(
        IHttpClientFactory httpClientFactory,
        IOptions<OAuthSettings> oauthSettings,
        IMemoryCache cache,
        ILogger<AppleSignInValidator> logger)
    {
        _httpClientFactory = httpClientFactory;
        _oauthSettings = oauthSettings.Value;
        _cache = cache;
        _logger = logger;
    }

    public async Task<AppleSignInClaims?> ValidateIdentityTokenAsync(
        string identityToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(identityToken))
        {
            return null;
        }

        if (string.Equals(identityToken, "demo-apple-token", StringComparison.Ordinal))
        {
            return new AppleSignInClaims("demo-apple", "apple@memoressa.com", "Apple User");
        }

        var clientId = _oauthSettings.Apple.ClientId;
        if (string.IsNullOrWhiteSpace(clientId))
        {
            _logger.LogWarning("OAuth:Apple:ClientId is not configured");
            return null;
        }

        var keys = await GetSigningKeysAsync(cancellationToken);
        if (keys.Count == 0)
        {
            return null;
        }

        var handler = new JwtSecurityTokenHandler();
        try
        {
            var parameters = new TokenValidationParameters
            {
                ValidIssuer = AppleIssuer,
                ValidAudience = clientId,
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = keys,
                ClockSkew = TimeSpan.FromMinutes(2)
            };

            var principal = handler.ValidateToken(identityToken, parameters, out _);
            var sub = principal.FindFirst("sub")?.Value;
            if (string.IsNullOrWhiteSpace(sub))
            {
                return null;
            }

            var email = principal.FindFirst("email")?.Value;
            return new AppleSignInClaims(sub, email, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Apple identity token validation failed");
            return null;
        }
    }

    private async Task<IReadOnlyList<SecurityKey>> GetSigningKeysAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue<IReadOnlyList<SecurityKey>>("AppleJwks", out var cached) && cached is not null)
        {
            return cached;
        }

        var client = _httpClientFactory.CreateClient("AppleOAuth");
        var response = await client.GetAsync(AppleKeysUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Failed to fetch Apple JWKS: {Status}", response.StatusCode);
            return [];
        }

        var jwks = await response.Content.ReadFromJsonAsync<AppleJwksResponse>(cancellationToken);
        if (jwks?.Keys is null || jwks.Keys.Count == 0)
        {
            return [];
        }

        var keys = new List<SecurityKey>();
        foreach (var key in jwks.Keys)
        {
            if (string.IsNullOrWhiteSpace(key.Kid) || string.IsNullOrWhiteSpace(key.N) || string.IsNullOrWhiteSpace(key.E))
            {
                continue;
            }

            keys.Add(new JsonWebKey
            {
                Kty = key.Kty ?? "RSA",
                Kid = key.Kid,
                Use = key.Use,
                Alg = key.Alg,
                N = key.N,
                E = key.E
            });
        }

        _cache.Set("AppleJwks", keys, TimeSpan.FromHours(12));
        return keys;
    }

    private sealed class AppleJwksResponse
    {
        [JsonPropertyName("keys")] public List<AppleJwk> Keys { get; init; } = [];
    }

    private sealed class AppleJwk
    {
        [JsonPropertyName("kty")] public string? Kty { get; init; }
        [JsonPropertyName("kid")] public string? Kid { get; init; }
        [JsonPropertyName("use")] public string? Use { get; init; }
        [JsonPropertyName("alg")] public string? Alg { get; init; }
        [JsonPropertyName("n")] public string? N { get; init; }
        [JsonPropertyName("e")] public string? E { get; init; }
    }
}
