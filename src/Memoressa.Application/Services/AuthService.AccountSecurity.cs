using Memoressa.Application.Common;
using Memoressa.Application.DTOs;
using Memoressa.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Application.Services;

public partial class AuthService
{
    public async Task<ServiceResult> ChangePasswordAsync(
        ChangePasswordRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return ServiceResult.Fail("Unauthorized", 401);
        }

        if (request.NewPassword != request.ConfirmPassword)
        {
            return ServiceResult.Fail("Passwords do not match", 400);
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return ServiceResult.Fail("New password is required", 400);
        }

        var user = await _db.UserAccounts
            .FirstOrDefaultAsync(u => u.Id == _currentUser.UserId.Value && u.IsActive, cancellationToken);
        if (user is null)
        {
            return ServiceResult.NotFound("User not found");
        }

        if (string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return ServiceResult.Fail("Password login is not available for this account", 400);
        }

        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return ServiceResult.Fail("Current password is incorrect", 401);
        }

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult.NoContent();
    }

    public async Task<ServiceResult> RequestEmailVerificationAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return ServiceResult.Fail("Unauthorized", 401);
        }

        var user = await _db.UserAccounts
            .FirstOrDefaultAsync(u => u.Id == _currentUser.UserId.Value && u.IsActive, cancellationToken);
        if (user is null)
        {
            return ServiceResult.NotFound("User not found");
        }

        var rateLimit = await CheckEmailVerificationRateLimitAsync(user.Id, cancellationToken);
        if (rateLimit is not null)
        {
            return rateLimit;
        }

        await InvalidateActiveEmailVerificationCodesAsync(user.Id, cancellationToken);

        var code = PasswordResetCodeRules.GenerateNumericCode();
        _db.EmailVerificationCodes.Add(new EmailVerificationCode
        {
            UserId = user.Id,
            CodeHash = _passwordHasher.Hash(code),
            ExpiresAt = DateTime.UtcNow.AddMinutes(PasswordResetCodeRules.TtlMinutes)
        });
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            await _emailService.SendEmailVerificationCodeAsync(user.Email, code, cancellationToken);
        }
        catch (Exception)
        {
            return ServiceResult.Fail("無法寄送驗證碼，請稍後再試", 503);
        }

        return ServiceResult.NoContent();
    }

    public async Task<ServiceResult> RequestEmailChangeAsync(
        EmailChangeRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return ServiceResult.Fail("Unauthorized", 401);
        }

        if (!PasswordResetCodeRules.TryNormalizeEmail(request.NewEmail, out var newEmail, out var formatError))
        {
            return ServiceResult.Fail(formatError!, 400);
        }

        var user = await _db.UserAccounts
            .FirstOrDefaultAsync(u => u.Id == _currentUser.UserId.Value && u.IsActive, cancellationToken);
        if (user is null)
        {
            return ServiceResult.NotFound("User not found");
        }

        if (string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return ServiceResult.Fail("Password login is not available for this account", 400);
        }

        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return ServiceResult.Fail("Current password is incorrect", 401);
        }

        if (string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult.Fail("New email must differ from current email", 400);
        }

        if (await _db.UserAccounts.AnyAsync(u => u.Email == newEmail && u.Id != user.Id, cancellationToken))
        {
            return ServiceResult.Fail("Email already in use", 409);
        }

        var rateLimit = await CheckEmailChangeRateLimitAsync(user.Id, cancellationToken);
        if (rateLimit is not null)
        {
            return rateLimit;
        }

        await InvalidateActiveEmailChangeCodesAsync(user.Id, cancellationToken);

        var code = PasswordResetCodeRules.GenerateNumericCode();
        _db.EmailChangeCodes.Add(new EmailChangeCode
        {
            UserId = user.Id,
            NewEmail = newEmail,
            CodeHash = _passwordHasher.Hash(code),
            ExpiresAt = DateTime.UtcNow.AddMinutes(PasswordResetCodeRules.TtlMinutes)
        });
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            await _emailService.SendEmailChangeCodeAsync(newEmail, code, cancellationToken);
        }
        catch (Exception)
        {
            return ServiceResult.Fail("無法寄送驗證碼，請稍後再試", 503);
        }

        return ServiceResult.NoContent();
    }

    public async Task<ServiceResult> ConfirmEmailChangeAsync(
        EmailChangeConfirmRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return ServiceResult.Fail("Unauthorized", 401);
        }

        if (!PasswordResetCodeRules.TryNormalizeEmail(request.NewEmail, out var newEmail, out var formatError))
        {
            return ServiceResult.Fail(formatError!, 400);
        }

        if (!PasswordResetCodeRules.IsValidCodeFormat(request.Code))
        {
            return ServiceResult.Fail("驗證碼格式不正確", 400);
        }

        var user = await _db.UserAccounts
            .FirstOrDefaultAsync(u => u.Id == _currentUser.UserId.Value && u.IsActive, cancellationToken);
        if (user is null)
        {
            return ServiceResult.NotFound("User not found");
        }

        if (await _db.UserAccounts.AnyAsync(u => u.Email == newEmail && u.Id != user.Id, cancellationToken))
        {
            return ServiceResult.Fail("Email already in use", 409);
        }

        var validation = await ValidateEmailChangeCodeAsync(user.Id, newEmail, request.Code.Trim(), cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        var activeCode = await FindLatestActiveEmailChangeCodeAsync(user.Id, newEmail, cancellationToken);
        if (activeCode is null)
        {
            return ServiceResult.Fail("驗證碼錯誤或已過期", 401);
        }

        user.Email = newEmail;
        user.EmailVerifiedAt = DateTime.UtcNow;
        activeCode.UsedAt = DateTime.UtcNow;
        await InvalidateActiveEmailChangeCodesAsync(user.Id, cancellationToken, exceptCodeId: activeCode.Id);
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult.NoContent();
    }

    private async Task<ServiceResult?> CheckEmailVerificationRateLimitAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var hourAgo = now.AddHours(-1);
        var sentLastHour = await _db.EmailVerificationCodes.AsNoTracking()
            .CountAsync(c => c.UserId == userId && c.CreatedAt >= hourAgo, cancellationToken);
        if (sentLastHour >= PasswordResetCodeRules.MaxCodesPerHour)
        {
            return ServiceResult.Fail("寄送驗證碼過於頻繁，請稍後再試", 429);
        }

        var lastSent = await _db.EmailVerificationCodes.AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => c.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (lastSent != default
            && lastSent > now.AddSeconds(-PasswordResetCodeRules.MinSecondsBetweenSends))
        {
            return ServiceResult.Fail("請稍候再索取驗證碼", 429);
        }

        return null;
    }

    private async Task<ServiceResult?> CheckEmailChangeRateLimitAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var hourAgo = now.AddHours(-1);
        var sentLastHour = await _db.EmailChangeCodes.AsNoTracking()
            .CountAsync(c => c.UserId == userId && c.CreatedAt >= hourAgo, cancellationToken);
        if (sentLastHour >= PasswordResetCodeRules.MaxCodesPerHour)
        {
            return ServiceResult.Fail("寄送驗證碼過於頻繁，請稍後再試", 429);
        }

        var lastSent = await _db.EmailChangeCodes.AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => c.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (lastSent != default
            && lastSent > now.AddSeconds(-PasswordResetCodeRules.MinSecondsBetweenSends))
        {
            return ServiceResult.Fail("請稍候再索取驗證碼", 429);
        }

        return null;
    }

    private async Task<ServiceResult?> ValidateEmailChangeCodeAsync(
        Guid userId,
        string newEmail,
        string code,
        CancellationToken cancellationToken)
    {
        var activeCode = await FindLatestActiveEmailChangeCodeAsync(userId, newEmail, cancellationToken);
        if (activeCode is null)
        {
            return ServiceResult.Fail("驗證碼錯誤或已過期", 401);
        }

        if (activeCode.FailedVerifyAttempts >= PasswordResetCodeRules.MaxFailedVerifyAttempts)
        {
            activeCode.UsedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return ServiceResult.Fail("驗證碼錯誤或已過期", 401);
        }

        if (!_passwordHasher.Verify(code, activeCode.CodeHash))
        {
            activeCode.FailedVerifyAttempts++;
            if (activeCode.FailedVerifyAttempts >= PasswordResetCodeRules.MaxFailedVerifyAttempts)
            {
                activeCode.UsedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync(cancellationToken);
            return ServiceResult.Fail("驗證碼錯誤或已過期", 401);
        }

        return null;
    }

    private async Task<EmailChangeCode?> FindLatestActiveEmailChangeCodeAsync(
        Guid userId,
        string newEmail,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        return await _db.EmailChangeCodes
            .Where(c => c.UserId == userId
                        && c.NewEmail == newEmail
                        && c.UsedAt == null
                        && c.ExpiresAt > now)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task InvalidateActiveEmailVerificationCodesAsync(
        Guid userId,
        CancellationToken cancellationToken,
        Guid? exceptCodeId = null)
    {
        var now = DateTime.UtcNow;
        var active = await _db.EmailVerificationCodes
            .Where(c => c.UserId == userId && c.UsedAt == null && c.ExpiresAt > now)
            .ToListAsync(cancellationToken);

        foreach (var row in active)
        {
            if (exceptCodeId.HasValue && row.Id == exceptCodeId.Value)
            {
                continue;
            }

            row.UsedAt = now;
        }
    }

    private async Task InvalidateActiveEmailChangeCodesAsync(
        Guid userId,
        CancellationToken cancellationToken,
        Guid? exceptCodeId = null)
    {
        var now = DateTime.UtcNow;
        var active = await _db.EmailChangeCodes
            .Where(c => c.UserId == userId && c.UsedAt == null && c.ExpiresAt > now)
            .ToListAsync(cancellationToken);

        foreach (var row in active)
        {
            if (exceptCodeId.HasValue && row.Id == exceptCodeId.Value)
            {
                continue;
            }

            row.UsedAt = now;
        }
    }
}
