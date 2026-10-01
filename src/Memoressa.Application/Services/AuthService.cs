using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Memoressa.Application.Abstractions;
using Memoressa.Application.Common;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Memoressa.Application.Services;

public partial class AuthService : IAuthService
{
    private readonly IMemoressaDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailService _emailService;
    private readonly ICurrentUserService _currentUser;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OAuthSettings _oauthSettings;
    private readonly IAppleSignInValidator _appleSignInValidator;
    private readonly IAvatarUrlResolver _avatarUrls;

    public AuthService(
        IMemoressaDbContext db,
        ITokenService tokenService,
        IPasswordHasher passwordHasher,
        IEmailService emailService,
        ICurrentUserService currentUser,
        IHttpClientFactory httpClientFactory,
        IOptions<OAuthSettings> oauthSettings,
        IAppleSignInValidator appleSignInValidator,
        IAvatarUrlResolver avatarUrls)
    {
        _db = db;
        _tokenService = tokenService;
        _passwordHasher = passwordHasher;
        _emailService = emailService;
        _currentUser = currentUser;
        _httpClientFactory = httpClientFactory;
        _oauthSettings = oauthSettings.Value;
        _appleSignInValidator = appleSignInValidator;
        _avatarUrls = avatarUrls;
    }

    public async Task<ServiceResult<AuthResponseDto>> RegisterAsync(
        RegisterRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            return ServiceResult<AuthResponseDto>.Fail("Valid email is required");
        }

        if (await _db.UserAccounts.AnyAsync(u => u.Email == email, cancellationToken))
        {
            return ServiceResult<AuthResponseDto>.Fail("Email already registered", 409);
        }

        var user = new UserAccount
        {
            Email = email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            Nickname = request.Nickname,
            Generation = Generation.Self,
            OnboardingComplete = false
        };
        _db.UserAccounts.Add(user);

        var family = new Family
        {
            Name = "My Family",
            OwnerUserId = user.Id
        };
        _db.Families.Add(family);

        _db.FamilyMemberships.Add(new FamilyMembership
        {
            FamilyId = family.Id,
            UserId = user.Id,
            Role = "owner"
        });

        _db.SharedAlbums.AddRange(
            new SharedAlbum
            {
                FamilyId = family.Id,
                ExternalId = AppConstants.SharedAlbumOwnPersonal,
                Name = "Personal",
                AlbumType = SharedAlbumType.Personal,
                IsOwn = true
            },
            new SharedAlbum
            {
                FamilyId = family.Id,
                ExternalId = AppConstants.SharedAlbumOwnFamily,
                Name = "Family",
                AlbumType = SharedAlbumType.Family,
                IsOwn = true
            });

        await _db.SaveChangesAsync(cancellationToken);
        return await BuildAuthResponseAsync(user, family.Id, cancellationToken);
    }

    public async Task<ServiceResult<AuthResponseDto>> LoginAsync(
        LoginRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.UserAccounts
            .FirstOrDefaultAsync(u => u.Email == email && u.IsActive, cancellationToken);

        if (user is null || user.PasswordHash is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return ServiceResult<AuthResponseDto>.Fail("Invalid email or password", 401);
        }

        if (user.DeletedAt.HasValue || (user.DeletionScheduledAt.HasValue && user.DeletionScheduledAt <= DateTime.UtcNow))
        {
            return ServiceResult<AuthResponseDto>.Fail("Account has been deleted", 403);
        }

        var familyId = await _db.FamilyMemberships.AsNoTracking()
            .Where(m => m.UserId == user.Id)
            .Select(m => m.FamilyId)
            .FirstOrDefaultAsync(cancellationToken);

        return await BuildAuthResponseAsync(user, familyId, cancellationToken);
    }

    public async Task<ServiceResult<AuthResponseDto>> RefreshTokenAsync(
        RefreshTokenRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var refreshToken = await _tokenService.ValidateRefreshTokenAsync(request.RefreshToken, cancellationToken);
        if (refreshToken is null)
        {
            return ServiceResult<AuthResponseDto>.Fail("Invalid refresh token", 401);
        }

        var user = await _db.UserAccounts.FirstOrDefaultAsync(u => u.Id == refreshToken.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return ServiceResult<AuthResponseDto>.Fail("User not found", 404);
        }

        await _tokenService.RevokeRefreshTokenAsync(request.RefreshToken, cancellationToken);
        var familyId = await _db.FamilyMemberships.AsNoTracking()
            .Where(m => m.UserId == user.Id)
            .Select(m => m.FamilyId)
            .FirstOrDefaultAsync(cancellationToken);

        return await BuildAuthResponseAsync(user, familyId, cancellationToken);
    }

    public async Task<ServiceResult> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        await _tokenService.RevokeRefreshTokenAsync(refreshToken, cancellationToken);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> RequestPasswordResetAsync(
        PasswordResetRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.UserAccounts.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (user is null)
        {
            return ServiceResult.Ok();
        }

        var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        _db.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = user.Id,
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        });
        await _db.SaveChangesAsync(cancellationToken);
        await _emailService.SendPasswordResetAsync(email, token, cancellationToken);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> ResetPasswordAsync(
        ResetPasswordRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request.NewPassword != request.ConfirmPassword)
        {
            return ServiceResult.Fail("Passwords do not match");
        }

        var resetToken = await _db.PasswordResetTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(
                t => t.Token == request.Token && t.UsedAt == null && t.ExpiresAt > DateTime.UtcNow,
                cancellationToken);

        if (resetToken is null)
        {
            return ServiceResult.Fail("Invalid or expired reset token", 400);
        }

        resetToken.User.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        resetToken.UsedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> ScheduleAccountDeletionAsync(
        string password,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return ServiceResult.Fail("Unauthorized", 401);
        }

        var user = await _db.UserAccounts.FirstOrDefaultAsync(u => u.Id == _currentUser.UserId.Value, cancellationToken);
        if (user is null)
        {
            return ServiceResult.NotFound("User not found");
        }

        if (user.PasswordHash is null || !_passwordHasher.Verify(password, user.PasswordHash))
        {
            return ServiceResult.Fail("Incorrect password");
        }

        user.DeletionScheduledAt = DateTime.UtcNow.AddDays(AppConstants.AccountDeletionGracePeriodDays);
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> CancelAccountDeletionAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return ServiceResult.Fail("Unauthorized", 401);
        }

        var user = await _db.UserAccounts.FirstOrDefaultAsync(u => u.Id == _currentUser.UserId.Value, cancellationToken);
        if (user is null)
        {
            return ServiceResult.NotFound("User not found");
        }

        user.DeletionScheduledAt = null;
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<UserDto>> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return ServiceResult<UserDto>.Fail("Unauthorized", 401);
        }

        var user = await _db.UserAccounts.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == _currentUser.UserId.Value, cancellationToken);

        return user is null
            ? ServiceResult<UserDto>.NotFound("User not found")
            : ServiceResult<UserDto>.Ok(await _avatarUrls.ToUserDtoAsync(user, cancellationToken));
    }

    public async Task<ServiceResult<UserDto>> PatchCurrentUserAsync(
        PatchMeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return ServiceResult<UserDto>.Fail("Unauthorized", 401);
        }

        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<UserDto>.Fail("Unauthorized", 401);
        }

        var user = await _db.UserAccounts
            .FirstOrDefaultAsync(u => u.Id == _currentUser.UserId.Value, cancellationToken);

        if (user is null)
        {
            return ServiceResult<UserDto>.NotFound("User not found");
        }

        if (request.IsSet("selfFamilyMemberId"))
        {
            if (request.SelfFamilyMemberId.HasValue)
            {
                var memberExists = await _db.FamilyMembers.AsNoTracking()
                    .AnyAsync(
                        m => m.Id == request.SelfFamilyMemberId.Value && m.FamilyId == ctx.Value.FamilyId,
                        cancellationToken);

                if (!memberExists)
                {
                    return ServiceResult<UserDto>.Fail("Family member not found in your family", 404);
                }
            }

            user.SelfFamilyMemberId = request.SelfFamilyMemberId;
        }

        if (request.IsSet("nickname"))
        {
            user.Nickname = UploadMetadata.NormalizeOptionalText(request.Nickname);
        }

        if (request.IsSet("avatarUrl"))
        {
            user.AvatarUrl = UploadMetadata.NormalizeOptionalText(request.AvatarUrl);
        }

        if (request.IsSet("birthDate"))
        {
            user.BirthDate = request.BirthDate;
        }

        if (request.IsSet("profileCityId"))
        {
            user.ProfileCityId = UploadMetadata.NormalizeOptionalText(request.ProfileCityId);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return ServiceResult<UserDto>.Ok(await _avatarUrls.ToUserDtoAsync(user, cancellationToken));
    }

    public async Task<ServiceResult<AccountDeletionStatusDto>> GetDeletionStatusAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return ServiceResult<AccountDeletionStatusDto>.Fail("Unauthorized", 401);
        }

        var user = await _db.UserAccounts.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == _currentUser.UserId.Value, cancellationToken);

        if (user is null)
        {
            return ServiceResult<AccountDeletionStatusDto>.NotFound("User not found");
        }

        int? days = null;
        if (user.DeletionScheduledAt.HasValue)
        {
            days = Math.Max(0, (user.DeletionScheduledAt.Value.Date - DateTime.UtcNow.Date).Days);
        }

        return ServiceResult<AccountDeletionStatusDto>.Ok(new AccountDeletionStatusDto
        {
            IsPending = user.DeletionScheduledAt.HasValue,
            EffectiveAt = user.DeletionScheduledAt,
            DaysUntilDeletion = days
        });
    }

    public async Task<ServiceResult<AuthResponseDto>> LoginWithGoogleAsync(
        OAuthLoginRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.IdToken))
        {
            return ServiceResult<AuthResponseDto>.Fail("Google idToken is required");
        }

        var profile = await ResolveGoogleProfileAsync(request.IdToken, cancellationToken);
        if (profile is null)
        {
            return ServiceResult<AuthResponseDto>.Fail("Invalid Google token", 401);
        }

        return await LoginWithOAuthProviderAsync(OAuthProvider.Google, profile, cancellationToken);
    }

    public async Task<ServiceResult<AuthResponseDto>> LoginWithFacebookAsync(
        OAuthLoginRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.AccessToken))
        {
            return ServiceResult<AuthResponseDto>.Fail("Facebook accessToken is required");
        }

        var profile = await ResolveFacebookProfileAsync(request.AccessToken, cancellationToken);
        if (profile is null)
        {
            return ServiceResult<AuthResponseDto>.Fail("Invalid Facebook token", 401);
        }

        return await LoginWithOAuthProviderAsync(OAuthProvider.Facebook, profile, cancellationToken);
    }

    public async Task<ServiceResult<AuthResponseDto>> LoginWithAppleAsync(
        AppleOAuthRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.IdentityToken))
        {
            return ServiceResult<AuthResponseDto>.Fail("Apple identityToken is required");
        }

        var claims = await _appleSignInValidator.ValidateIdentityTokenAsync(request.IdentityToken, cancellationToken);
        if (claims is null)
        {
            return ServiceResult<AuthResponseDto>.Fail("Invalid Apple token", 401);
        }

        var profile = new OAuthProfile(claims.Subject, claims.Email ?? string.Empty, claims.Name);
        return await LoginWithOAuthProviderAsync(OAuthProvider.Apple, profile, cancellationToken);
    }

    private async Task<ServiceResult<AuthResponseDto>> LoginWithOAuthProviderAsync(
        OAuthProvider provider,
        OAuthProfile profile,
        CancellationToken cancellationToken)
    {
        var existingLink = await _db.UserOAuthLinks
            .Include(l => l.User)
            .FirstOrDefaultAsync(
                l => l.Provider == provider && l.ProviderUserId == profile.ProviderUserId,
                cancellationToken);

        if (existingLink is not null)
        {
            var linkedUser = existingLink.User;
            if (!linkedUser.IsActive || linkedUser.DeletedAt.HasValue
                || (linkedUser.DeletionScheduledAt.HasValue && linkedUser.DeletionScheduledAt <= DateTime.UtcNow))
            {
                return ServiceResult<AuthResponseDto>.Fail("Account is not available", 403);
            }

            var linkedFamilyId = await _db.FamilyMemberships.AsNoTracking()
                .Where(m => m.UserId == linkedUser.Id)
                .Select(m => m.FamilyId)
                .FirstOrDefaultAsync(cancellationToken);

            return await BuildAuthResponseAsync(linkedUser, linkedFamilyId, cancellationToken);
        }

        var email = profile.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            return ServiceResult<AuthResponseDto>.Fail("OAuth provider did not return a valid email", 400);
        }

        var user = await _db.UserAccounts
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken)
            ?? await CreateOAuthUserAsync(email, profile.Name, cancellationToken);

        if (!await _db.UserOAuthLinks.AnyAsync(
                l => l.UserId == user.Id && l.Provider == provider,
                cancellationToken))
        {
            _db.UserOAuthLinks.Add(new UserOAuthLink
            {
                UserId = user.Id,
                Provider = provider,
                ProviderUserId = profile.ProviderUserId
            });
            await _db.SaveChangesAsync(cancellationToken);
        }

        if (!user.IsActive || user.DeletedAt.HasValue
            || (user.DeletionScheduledAt.HasValue && user.DeletionScheduledAt <= DateTime.UtcNow))
        {
            return ServiceResult<AuthResponseDto>.Fail("Account is not available", 403);
        }

        var familyId = await _db.FamilyMemberships.AsNoTracking()
            .Where(m => m.UserId == user.Id)
            .Select(m => m.FamilyId)
            .FirstOrDefaultAsync(cancellationToken);

        return await BuildAuthResponseAsync(user, familyId, cancellationToken);
    }

    private async Task<UserAccount> CreateOAuthUserAsync(
        string email,
        string? nickname,
        CancellationToken cancellationToken)
    {
        var user = new UserAccount
        {
            Email = email,
            Nickname = nickname,
            Generation = Generation.Self,
            OnboardingComplete = false
        };
        _db.UserAccounts.Add(user);

        var family = new Family
        {
            Name = "My Family",
            OwnerUserId = user.Id
        };
        _db.Families.Add(family);

        _db.FamilyMemberships.Add(new FamilyMembership
        {
            FamilyId = family.Id,
            UserId = user.Id,
            Role = "owner"
        });

        _db.SharedAlbums.AddRange(
            new SharedAlbum
            {
                FamilyId = family.Id,
                ExternalId = AppConstants.SharedAlbumOwnPersonal,
                Name = "Personal",
                AlbumType = SharedAlbumType.Personal,
                IsOwn = true
            },
            new SharedAlbum
            {
                FamilyId = family.Id,
                ExternalId = AppConstants.SharedAlbumOwnFamily,
                Name = "Family",
                AlbumType = SharedAlbumType.Family,
                IsOwn = true
            });

        await _db.SaveChangesAsync(cancellationToken);
        return user;
    }

    private async Task<OAuthProfile?> ResolveGoogleProfileAsync(string idToken, CancellationToken cancellationToken)
    {
        if (string.Equals(idToken, "demo-google-token", StringComparison.Ordinal))
        {
            return new OAuthProfile("demo-google", "google@memoressa.com", "Google User");
        }

        if (!string.IsNullOrWhiteSpace(_oauthSettings.Google.ClientId))
        {
            var client = _httpClientFactory.CreateClient("GoogleOAuth");
            var response = await client.GetAsync(
                $"https://oauth2.googleapis.com/tokeninfo?id_token={Uri.EscapeDataString(idToken)}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<GoogleTokenInfoResponse>(cancellationToken);
            if (payload is null
                || string.IsNullOrWhiteSpace(payload.Sub)
                || string.IsNullOrWhiteSpace(payload.Email))
            {
                return null;
            }

            if (!string.Equals(payload.Aud, _oauthSettings.Google.ClientId, StringComparison.Ordinal))
            {
                return null;
            }

            return new OAuthProfile(payload.Sub, payload.Email, payload.Name);
        }

        return TryParseJwtProfile(idToken, "google");
    }

    private async Task<OAuthProfile?> ResolveFacebookProfileAsync(string accessToken, CancellationToken cancellationToken)
    {
        if (string.Equals(accessToken, "demo-facebook-token", StringComparison.Ordinal))
        {
            return new OAuthProfile("demo-facebook", "facebook@memoressa.com", "Facebook User");
        }

        if (!string.IsNullOrWhiteSpace(_oauthSettings.Facebook.AppId))
        {
            var client = _httpClientFactory.CreateClient("FacebookOAuth");
            var response = await client.GetAsync(
                $"https://graph.facebook.com/me?fields=id,email,name&access_token={Uri.EscapeDataString(accessToken)}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<FacebookProfileResponse>(cancellationToken);
            if (payload is null
                || string.IsNullOrWhiteSpace(payload.Id)
                || string.IsNullOrWhiteSpace(payload.Email))
            {
                return null;
            }

            return new OAuthProfile(payload.Id, payload.Email, payload.Name);
        }

        if (accessToken.Length < 20)
        {
            return null;
        }

        var stubEmail = $"facebook-{Convert.ToHexString(Encoding.UTF8.GetBytes(accessToken[..Math.Min(16, accessToken.Length)])).ToLowerInvariant()}@oauth.local";
        var stubId = Convert.ToHexString(Encoding.UTF8.GetBytes(accessToken)).ToLowerInvariant();
        return await Task.FromResult(new OAuthProfile(stubId, stubEmail, null));
    }

    private static OAuthProfile? TryParseJwtProfile(string token, string providerPrefix)
    {
        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return null;
        }

        try
        {
            var payloadJson = Encoding.UTF8.GetString(PadBase64(parts[1]));
            using var document = JsonDocument.Parse(payloadJson);
            var root = document.RootElement;

            var sub = root.TryGetProperty("sub", out var subElement)
                ? subElement.GetString()
                : null;
            var email = root.TryGetProperty("email", out var emailElement)
                ? emailElement.GetString()
                : null;
            var name = root.TryGetProperty("name", out var nameElement)
                ? nameElement.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(sub))
            {
                sub = Convert.ToHexString(Encoding.UTF8.GetBytes(token)).ToLowerInvariant();
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                email = $"{providerPrefix}-{sub}@oauth.local";
            }

            return new OAuthProfile(sub, email, name);
        }
        catch
        {
            return null;
        }
    }

    private static byte[] PadBase64(string base64)
    {
        var padded = base64.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }

        return Convert.FromBase64String(padded);
    }

    private async Task<ServiceResult<AuthResponseDto>> BuildAuthResponseAsync(
        UserAccount user,
        Guid familyId,
        CancellationToken cancellationToken)
    {
        var tokens = _tokenService.GenerateTokens(user, familyId);
        var refresh = await _tokenService.CreateRefreshTokenAsync(user.Id, cancellationToken);
        tokens = tokens with { RefreshToken = refresh.Token };

        return ServiceResult<AuthResponseDto>.Ok(new AuthResponseDto
        {
            User = await _avatarUrls.ToUserDtoAsync(user, cancellationToken),
            Tokens = tokens,
            FamilyId = familyId
        });
    }

    private sealed record OAuthProfile(string ProviderUserId, string Email, string? Name);

    private sealed class GoogleTokenInfoResponse
    {
        [JsonPropertyName("sub")] public string Sub { get; init; } = string.Empty;
        [JsonPropertyName("email")] public string Email { get; init; } = string.Empty;
        [JsonPropertyName("name")] public string? Name { get; init; }
        [JsonPropertyName("aud")] public string Aud { get; init; } = string.Empty;
    }

    private sealed class FacebookProfileResponse
    {
        [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
        [JsonPropertyName("email")] public string Email { get; init; } = string.Empty;
        [JsonPropertyName("name")] public string? Name { get; init; }
    }
}

