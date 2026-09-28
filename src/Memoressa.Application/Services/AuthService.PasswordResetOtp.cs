using Memoressa.Application.Common;
using Memoressa.Application.DTOs;
using Memoressa.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Application.Services;

public partial class AuthService
{
    public async Task<ServiceResult> RequestPasswordResetCodeAsync(
        PasswordResetEmailRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!PasswordResetCodeRules.TryNormalizeEmail(request.Email, out var email, out var formatError))
        {
            return ServiceResult.Fail(formatError!, 400);
        }

        var user = await _db.UserAccounts.FirstOrDefaultAsync(u => u.Email == email && u.IsActive, cancellationToken);
        if (user is null)
        {
            return ServiceResult.NotFound("找不到此 email");
        }

        var rateLimit = await CheckPasswordResetCodeRateLimitAsync(user.Id, cancellationToken);
        if (rateLimit is not null)
        {
            return rateLimit;
        }

        await InvalidateActivePasswordResetCodesAsync(user.Id, cancellationToken);

        var code = PasswordResetCodeRules.GenerateNumericCode();
        _db.PasswordResetCodes.Add(new PasswordResetCode
        {
            UserId = user.Id,
            CodeHash = _passwordHasher.Hash(code),
            ExpiresAt = DateTime.UtcNow.AddMinutes(PasswordResetCodeRules.TtlMinutes)
        });
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            await _emailService.SendPasswordResetCodeAsync(email, code, cancellationToken);
        }
        catch (Exception)
        {
            return ServiceResult.Fail("無法寄送驗證碼，請稍後再試", 503);
        }

        return ServiceResult.NoContent();
    }

    public async Task<ServiceResult> VerifyPasswordResetCodeAsync(
        PasswordResetCodeVerifyRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!PasswordResetCodeRules.TryNormalizeEmail(request.Email, out var email, out var formatError))
        {
            return ServiceResult.Fail(formatError!, 400);
        }

        if (!PasswordResetCodeRules.IsValidCodeFormat(request.Code))
        {
            return ServiceResult.Fail("驗證碼格式不正確", 400);
        }

        var user = await _db.UserAccounts.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email && u.IsActive, cancellationToken);
        if (user is null)
        {
            return ServiceResult.Fail("驗證碼錯誤或已過期", 401);
        }

        var validation = await ValidatePasswordResetCodeAsync(user.Id, request.Code.Trim(), cancellationToken);
        return validation ?? ServiceResult.NoContent();
    }

    public async Task<ServiceResult> ConfirmPasswordResetWithCodeAsync(
        PasswordResetCodeConfirmRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!PasswordResetCodeRules.TryNormalizeEmail(request.Email, out var email, out var formatError))
        {
            return ServiceResult.Fail(formatError!, 400);
        }

        if (!PasswordResetCodeRules.IsValidCodeFormat(request.Code))
        {
            return ServiceResult.Fail("驗證碼格式不正確", 400);
        }

        if (request.NewPassword != request.ConfirmPassword)
        {
            return ServiceResult.Fail("Passwords do not match", 400);
        }

        var user = await _db.UserAccounts
            .FirstOrDefaultAsync(u => u.Email == email && u.IsActive, cancellationToken);
        if (user is null)
        {
            return ServiceResult.Fail("驗證碼錯誤或已過期", 401);
        }

        var validation = await ValidatePasswordResetCodeAsync(user.Id, request.Code.Trim(), cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        var activeCode = await FindLatestActivePasswordResetCodeAsync(user.Id, cancellationToken);
        if (activeCode is null)
        {
            return ServiceResult.Fail("驗證碼錯誤或已過期", 401);
        }

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        activeCode.UsedAt = DateTime.UtcNow;
        await InvalidateActivePasswordResetCodesAsync(user.Id, cancellationToken, exceptCodeId: activeCode.Id);
        await _db.SaveChangesAsync(cancellationToken);

        return ServiceResult.NoContent();
    }

    private async Task<ServiceResult?> CheckPasswordResetCodeRateLimitAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var hourAgo = now.AddHours(-1);
        var sentLastHour = await _db.PasswordResetCodes.AsNoTracking()
            .CountAsync(c => c.UserId == userId && c.CreatedAt >= hourAgo, cancellationToken);

        if (sentLastHour >= PasswordResetCodeRules.MaxCodesPerHour)
        {
            return ServiceResult.Fail("寄送驗證碼過於頻繁，請稍後再試", 429);
        }

        var lastSent = await _db.PasswordResetCodes.AsNoTracking()
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

    private async Task<ServiceResult?> ValidatePasswordResetCodeAsync(
        Guid userId,
        string code,
        CancellationToken cancellationToken)
    {
        var activeCode = await FindLatestActivePasswordResetCodeAsync(userId, cancellationToken);
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

    private async Task<PasswordResetCode?> FindLatestActivePasswordResetCodeAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        return await _db.PasswordResetCodes
            .Where(c => c.UserId == userId && c.UsedAt == null && c.ExpiresAt > now)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task InvalidateActivePasswordResetCodesAsync(
        Guid userId,
        CancellationToken cancellationToken,
        Guid? exceptCodeId = null)
    {
        var now = DateTime.UtcNow;
        var active = await _db.PasswordResetCodes
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
