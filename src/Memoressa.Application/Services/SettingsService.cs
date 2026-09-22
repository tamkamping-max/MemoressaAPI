using Memoressa.Application.Abstractions;
using Memoressa.Application.Common;
using Memoressa.Application.Interfaces;
using Memoressa.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Application.Services;

public class SettingsService : ISettingsService
{
    private readonly IMemoressaDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public SettingsService(IMemoressaDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ServiceResult<string>> GetLocaleAsync(CancellationToken cancellationToken = default)
    {
        var user = await GetUserAsync(cancellationToken);
        if (user is null)
        {
            return ServiceResult<string>.Fail("Unauthorized", 401);
        }

        return ServiceResult<string>.Ok(user.Locale);
    }

    public async Task<ServiceResult> SetLocaleAsync(string locale, CancellationToken cancellationToken = default)
    {
        var user = await GetUserAsync(cancellationToken);
        if (user is null)
        {
            return ServiceResult.Fail("Unauthorized", 401);
        }

        user.Locale = locale;
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<bool>> IsOnboardingCompleteAsync(CancellationToken cancellationToken = default)
    {
        var user = await GetUserAsync(cancellationToken);
        if (user is null)
        {
            return ServiceResult<bool>.Fail("Unauthorized", 401);
        }

        return ServiceResult<bool>.Ok(user.OnboardingComplete);
    }

    public async Task<ServiceResult> SetOnboardingCompleteAsync(bool value, CancellationToken cancellationToken = default)
    {
        var user = await GetUserAsync(cancellationToken);
        if (user is null)
        {
            return ServiceResult.Fail("Unauthorized", 401);
        }

        user.OnboardingComplete = value;
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<Dictionary<string, bool>>> GetAiSettingsAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return ServiceResult<Dictionary<string, bool>>.Fail("Unauthorized", 401);
        }

        var settings = await _db.UserAiSettings.AsNoTracking()
            .Where(s => s.UserId == _currentUser.UserId.Value)
            .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);

        return ServiceResult<Dictionary<string, bool>>.Ok(settings);
    }

    public async Task<ServiceResult> UpdateAiSettingsAsync(
        Dictionary<string, bool> settings,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return ServiceResult.Fail("Unauthorized", 401);
        }

        var existing = await _db.UserAiSettings
            .Where(s => s.UserId == _currentUser.UserId.Value)
            .ToListAsync(cancellationToken);

        _db.UserAiSettings.RemoveRange(existing);
        foreach (var (key, value) in settings)
        {
            _db.UserAiSettings.Add(new UserAiSetting
            {
                UserId = _currentUser.UserId.Value,
                Key = key,
                Value = value
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok();
    }

    private async Task<UserAccount?> GetUserAsync(CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return null;
        }

        return await _db.UserAccounts.FirstOrDefaultAsync(u => u.Id == _currentUser.UserId.Value, cancellationToken);
    }
}
