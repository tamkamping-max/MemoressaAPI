using System.Text.Json.Serialization;
using Memoressa.Domain.Enums;

namespace Memoressa.Application.DTOs;

public record UserDto
{
    [JsonPropertyName("id")] public Guid Id { get; init; }
    [JsonPropertyName("email")] public string Email { get; init; } = string.Empty;
    [JsonPropertyName("nickname")] public string? Nickname { get; init; }
    [JsonPropertyName("avatarUrl")] public string? AvatarUrl { get; init; }
    [JsonPropertyName("generation")] public Generation? Generation { get; init; }
    [JsonPropertyName("createdAt")] public DateTime CreatedAt { get; init; }
}

public record AuthTokensDto
{
    [JsonPropertyName("accessToken")] public string AccessToken { get; init; } = string.Empty;
    [JsonPropertyName("refreshToken")] public string RefreshToken { get; init; } = string.Empty;
    [JsonPropertyName("expiresAt")] public DateTime ExpiresAt { get; init; }
}

public record AuthResponseDto
{
    [JsonPropertyName("user")] public UserDto User { get; init; } = null!;
    [JsonPropertyName("tokens")] public AuthTokensDto Tokens { get; init; } = null!;
    [JsonPropertyName("familyId")] public Guid FamilyId { get; init; }
}

public record RegisterRequestDto
{
    [JsonPropertyName("email")] public string Email { get; init; } = string.Empty;
    [JsonPropertyName("password")] public string Password { get; init; } = string.Empty;
    [JsonPropertyName("nickname")] public string? Nickname { get; init; }
}

public record LoginRequestDto
{
    [JsonPropertyName("email")] public string Email { get; init; } = string.Empty;
    [JsonPropertyName("password")] public string Password { get; init; } = string.Empty;
}

public record RefreshTokenRequestDto
{
    [JsonPropertyName("refreshToken")] public string RefreshToken { get; init; } = string.Empty;
}

public record PasswordResetRequestDto
{
    [JsonPropertyName("email")] public string Email { get; init; } = string.Empty;
}

public record PasswordResetEmailRequestDto
{
    [JsonPropertyName("email")] public string Email { get; init; } = string.Empty;
}

public record PasswordResetCodeVerifyRequestDto
{
    [JsonPropertyName("email")] public string Email { get; init; } = string.Empty;
    [JsonPropertyName("code")] public string Code { get; init; } = string.Empty;
}

public record PasswordResetCodeConfirmRequestDto
{
    [JsonPropertyName("email")] public string Email { get; init; } = string.Empty;
    [JsonPropertyName("code")] public string Code { get; init; } = string.Empty;
    [JsonPropertyName("newPassword")] public string NewPassword { get; init; } = string.Empty;
    [JsonPropertyName("confirmPassword")] public string ConfirmPassword { get; init; } = string.Empty;
}

public record ResetPasswordRequestDto
{
    [JsonPropertyName("token")] public string Token { get; init; } = string.Empty;
    [JsonPropertyName("newPassword")] public string NewPassword { get; init; } = string.Empty;
    [JsonPropertyName("confirmPassword")] public string ConfirmPassword { get; init; } = string.Empty;
}

public record AccountDeletionStatusDto
{
    [JsonPropertyName("isPending")] public bool IsPending { get; init; }
    [JsonPropertyName("effectiveAt")] public DateTime? EffectiveAt { get; init; }
    [JsonPropertyName("daysUntilDeletion")] public int? DaysUntilDeletion { get; init; }
}

public record AccountDeletionRequestDto
{
    [JsonPropertyName("password")] public string Password { get; init; } = string.Empty;
}

public record OAuthLoginRequestDto
{
    [JsonPropertyName("idToken")] public string? IdToken { get; init; }
    [JsonPropertyName("accessToken")] public string? AccessToken { get; init; }
}

public record AppleOAuthRequestDto
{
    [JsonPropertyName("identityToken")] public string IdentityToken { get; init; } = string.Empty;
}
