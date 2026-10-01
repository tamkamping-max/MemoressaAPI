using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Memoressa.Application.Abstractions;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Domain.Entities;
using Memoressa.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Memoressa.Infrastructure.Services;

public class TokenService : ITokenService
{
    private readonly JwtOptions _options;
    private readonly IMemoressaDbContext _db;

    public TokenService(IOptions<JwtOptions> options, IMemoressaDbContext db)
    {
        _options = options.Value;
        _db = db;
    }

    public AuthTokensDto GenerateTokens(UserAccount user, Guid familyId)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("family_id", familyId.ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return new AuthTokensDto
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
            RefreshToken = string.Empty,
            ExpiresAt = expiresAt
        };
    }

    public Guid? ValidateAccessToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        try
        {
            var principal = handler.ValidateToken(token, TokenValidationParameters, out _);
            var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                      ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(sub, out var userId) ? userId : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<RefreshToken?> ValidateRefreshTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        return await _db.RefreshTokens
            .FirstOrDefaultAsync(
                rt => rt.Token == token && rt.RevokedAt == null && rt.ExpiresAt > DateTime.UtcNow,
                cancellationToken);
    }

    public async Task<RefreshToken> CreateRefreshTokenAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var refreshToken = new RefreshToken
        {
            UserId = userId,
            Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            ExpiresAt = DateTime.UtcNow.AddDays(_options.RefreshTokenDays)
        };

        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync(cancellationToken);
        return refreshToken;
    }

    public async Task RevokeRefreshTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var refreshToken = await _db.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == token, cancellationToken);
        if (refreshToken is null)
        {
            return;
        }

        refreshToken.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private TokenValidationParameters TokenValidationParameters => new()
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = _options.Issuer,
        ValidAudience = _options.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey)),
        ClockSkew = TimeSpan.FromMinutes(1)
    };
}
