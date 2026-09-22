using Memoressa.Application.Abstractions;
using Memoressa.Application.Common;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Application.Services;

public class AuthService : IAuthService
{
    private readonly IMemoressaDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailService _emailService;
    private readonly ICurrentUserService _currentUser;

    public AuthService(
        IMemoressaDbContext db,
        ITokenService tokenService,
        IPasswordHasher passwordHasher,
        IEmailService emailService,
        ICurrentUserService currentUser)
    {
        _db = db;
        _tokenService = tokenService;
        _passwordHasher = passwordHasher;
        _emailService = emailService;
        _currentUser = currentUser;
    }

    public async Task<ServiceResult<AuthResponseDto>> RegisterAsync(
        RegisterRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request.Password != request.ConfirmPassword)
        {
            return ServiceResult<AuthResponseDto>.Fail("Passwords do not match");
        }

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
            : ServiceResult<UserDto>.Ok(user.ToDto());
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

    private async Task<ServiceResult<AuthResponseDto>> BuildAuthResponseAsync(
        UserAccount user,
        Guid familyId,
        CancellationToken cancellationToken)
    {
        var tokens = _tokenService.GenerateTokens(user);
        var refresh = await _tokenService.CreateRefreshTokenAsync(user.Id, cancellationToken);
        tokens = tokens with { RefreshToken = refresh.Token };

        return ServiceResult<AuthResponseDto>.Ok(new AuthResponseDto
        {
            User = user.ToDto(),
            Tokens = tokens,
            FamilyId = familyId
        });
    }
}
