using Memoressa.Application.Abstractions;
using Memoressa.Application.Common;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Application.Services;

public class NotificationService : INotificationService
{
    private readonly IMemoressaDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public NotificationService(IMemoressaDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ServiceResult<IReadOnlyList<NotificationDto>>> GetNotificationsAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return ServiceResult<IReadOnlyList<NotificationDto>>.Fail("Unauthorized", 401);
        }

        var notifications = await _db.Notifications.AsNoTracking()
            .Where(n => n.UserId == _currentUser.UserId.Value)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<NotificationDto>>.Ok(notifications.Select(n => n.ToDto()).ToList());
    }

    public async Task<ServiceResult> MarkAsReadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return ServiceResult.Fail("Unauthorized", 401);
        }

        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == _currentUser.UserId.Value, cancellationToken);

        if (notification is null)
        {
            return ServiceResult.NotFound("Notification not found");
        }

        notification.IsRead = true;
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> MarkAllAsReadAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return ServiceResult.Fail("Unauthorized", 401);
        }

        var notifications = await _db.Notifications
            .Where(n => n.UserId == _currentUser.UserId.Value && !n.IsRead)
            .ToListAsync(cancellationToken);

        foreach (var notification in notifications)
        {
            notification.IsRead = true;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok();
    }
}
