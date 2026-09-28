using Memoressa.Application.Abstractions;
using Memoressa.Application.Common;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Application.Services;

public class InternalRealtimeService : IInternalRealtimeService
{
    private const int CommandPollBatchSize = 50;

    private readonly IMemoressaDbContext _db;

    public InternalRealtimeService(IMemoressaDbContext db)
    {
        _db = db;
    }

    public async Task<ServiceResult> UpdateDeviceStatusAsync(
        UpdateDeviceStatusRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var device = await _db.DisplayDevices
            .FirstOrDefaultAsync(d => d.Id == request.DeviceId, cancellationToken);

        if (device is null)
        {
            return ServiceResult.NotFound("Device not found");
        }

        device.Status = request.Status;
        device.CurrentMemoryId = request.CurrentMemoryId;
        device.LastSeenAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<IReadOnlyList<FrameCommandDto>>> GetPendingCommandsAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        var commands = await _db.FrameCommands
            .Where(c => c.DisplayDeviceId == deviceId && c.Status == FrameCommandStatus.Pending)
            .OrderBy(c => c.CreatedAt)
            .Take(CommandPollBatchSize)
            .ToListAsync(cancellationToken);

        foreach (var command in commands)
        {
            command.Status = FrameCommandStatus.Delivered;
            command.DeliveredAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult<IReadOnlyList<FrameCommandDto>>.Ok(commands.Select(c => c.ToDto()).ToList());
    }

    public async Task<ServiceResult> AcknowledgeCommandAsync(
        Guid commandId,
        AckFrameCommandRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var command = await _db.FrameCommands.FirstOrDefaultAsync(c => c.Id == commandId, cancellationToken);
        if (command is null)
        {
            return ServiceResult.NotFound("Command not found");
        }

        command.Status = request.Success ? FrameCommandStatus.Acknowledged : FrameCommandStatus.Failed;
        command.AcknowledgedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<IReadOnlyList<FramePlaybackPackageDto>>> GetDevicePlaybackPackagesAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        var packages = await _db.FramePlaybackPackages.AsNoTracking()
            .Where(p => p.DisplayDeviceId == deviceId && p.IsActive)
            .OrderBy(p => p.SortOrder)
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<FramePlaybackPackageDto>>.Ok(packages.Select(p => p.ToDto()).ToList());
    }
}
