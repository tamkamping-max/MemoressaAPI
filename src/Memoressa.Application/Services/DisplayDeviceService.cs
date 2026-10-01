using System.Text.Json;
using Memoressa.Application.Abstractions;
using Memoressa.Application.Common;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Application.Services;

public class DisplayDeviceService : IDisplayDeviceService
{
    private const int SendActivityPhotosLimit = 500;

    private static readonly TimeSpan PairingSessionTtl = TimeSpan.FromMinutes(10);

    private readonly IMemoressaDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAiOrchestrationService _aiOrchestrationService;
    private readonly IDisplayDevicePairingSessionStore _pairingSessions;
    private readonly IFrameDeviceWebSocketHub _webSocketHub;

    public DisplayDeviceService(
        IMemoressaDbContext db,
        ICurrentUserService currentUser,
        IAiOrchestrationService aiOrchestrationService,
        IDisplayDevicePairingSessionStore pairingSessions,
        IFrameDeviceWebSocketHub webSocketHub)
    {
        _db = db;
        _currentUser = currentUser;
        _aiOrchestrationService = aiOrchestrationService;
        _pairingSessions = pairingSessions;
        _webSocketHub = webSocketHub;
    }

    public async Task<ServiceResult<IReadOnlyList<DisplayDeviceDto>>> GetDevicesAsync(CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<IReadOnlyList<DisplayDeviceDto>>.Fail("Unauthorized", 401);
        }

        var devices = await _db.DisplayDevices.AsNoTracking()
            .Where(d => d.FamilyId == ctx.Value.FamilyId)
            .OrderBy(d => d.Name)
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<DisplayDeviceDto>>.Ok(devices.Select(d => d.ToDto()).ToList());
    }

    public async Task<ServiceResult<DisplayDeviceDto>> CreateDeviceAsync(
        CreateDisplayDeviceRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<DisplayDeviceDto>.Fail("Unauthorized", 401);
        }

        var count = await _db.DisplayDevices.CountAsync(d => d.FamilyId == ctx.Value.FamilyId, cancellationToken);
        if (count >= AppConstants.MaxDisplayDevices)
        {
            return ServiceResult<DisplayDeviceDto>.Fail($"Maximum {AppConstants.MaxDisplayDevices} devices allowed");
        }

        var device = new DisplayDevice
        {
            FamilyId = ctx.Value.FamilyId,
            BoundByUserId = ctx.Value.UserId,
            Name = string.IsNullOrWhiteSpace(request.Name) ? $"Frame {count + 1}" : request.Name,
            QrCode = Guid.NewGuid().ToString(),
            Status = DisplayDeviceStatus.Offline
        };

        _db.DisplayDevices.Add(device);
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult<DisplayDeviceDto>.Ok(device.ToDto());
    }

    public async Task<ServiceResult<DisplayDeviceDto>> BindDeviceAsync(
        BindDisplayDeviceRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<DisplayDeviceDto>.Fail("Unauthorized", 401);
        }

        var count = await _db.DisplayDevices.CountAsync(d => d.FamilyId == ctx.Value.FamilyId, cancellationToken);
        if (count >= AppConstants.MaxDisplayDevices)
        {
            return ServiceResult<DisplayDeviceDto>.Fail($"Maximum {AppConstants.MaxDisplayDevices} devices allowed");
        }

        if (!TryNormalizePairingQrCode(request.QrCode, out var qrCode, out var qrError))
        {
            return ServiceResult<DisplayDeviceDto>.Fail(qrError!, 400);
        }

        var existing = await _db.DisplayDevices
            .FirstOrDefaultAsync(d => d.QrCode == qrCode, cancellationToken);

        if (existing is not null)
        {
            if (existing.FamilyId != ctx.Value.FamilyId)
            {
                return ServiceResult<DisplayDeviceDto>.Fail("Device already bound to another family", 409);
            }

            existing.Status = DisplayDeviceStatus.Online;
            existing.LastSeenAt = DateTime.UtcNow;
            existing.BoundByUserId = ctx.Value.UserId;
            if (!string.IsNullOrWhiteSpace(request.Name))
            {
                existing.Name = request.Name;
            }

            await _db.SaveChangesAsync(cancellationToken);
            _pairingSessions.Remove(qrCode);
            return ServiceResult<DisplayDeviceDto>.Ok(existing.ToDto());
        }

        if (!_pairingSessions.IsActive(qrCode))
        {
            return ServiceResult<DisplayDeviceDto>.Fail(
                "This QR code is not from an active Memoressa frame pairing screen",
                400);
        }

        var device = new DisplayDevice
        {
            FamilyId = ctx.Value.FamilyId,
            BoundByUserId = ctx.Value.UserId,
            Name = request.Name ?? $"Frame {count + 1}",
            QrCode = qrCode,
            Status = DisplayDeviceStatus.Online,
            LastSeenAt = DateTime.UtcNow
        };

        _db.DisplayDevices.Add(device);
        await _db.SaveChangesAsync(cancellationToken);
        _pairingSessions.Remove(qrCode);
        return ServiceResult<DisplayDeviceDto>.Ok(device.ToDto());
    }

    public async Task<ServiceResult<DisplayDeviceDto>> RenameDeviceAsync(
        Guid id,
        RenameDisplayDeviceRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<DisplayDeviceDto>.Fail("Unauthorized", 401);
        }

        var device = await _db.DisplayDevices
            .FirstOrDefaultAsync(d => d.Id == id && d.FamilyId == ctx.Value.FamilyId, cancellationToken);

        if (device is null)
        {
            return ServiceResult<DisplayDeviceDto>.NotFound("Device not found");
        }

        device.Name = request.Name;
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult<DisplayDeviceDto>.Ok(device.ToDto());
    }

    public async Task<ServiceResult> UnbindDeviceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult.Fail("Unauthorized", 401);
        }

        var device = await _db.DisplayDevices
            .FirstOrDefaultAsync(d => d.Id == id && d.FamilyId == ctx.Value.FamilyId, cancellationToken);

        if (device is null)
        {
            return ServiceResult.NotFound("Device not found");
        }

        _db.FrameCommands.Add(new FrameCommand
        {
            DisplayDeviceId = device.Id,
            IssuedByUserId = ctx.Value.UserId,
            CommandType = FrameCommandType.ClearFamilySharedContent,
            Status = FrameCommandStatus.Pending,
            PayloadJson = "{}"
        });

        var packages = await _db.FramePlaybackPackages
            .Where(p => p.DisplayDeviceId == device.Id)
            .ToListAsync(cancellationToken);

        foreach (var package in packages)
        {
            if (FramePlaybackPackageRetention.ShouldRemoveOnDeviceUnbind(package.PackageJson))
            {
                _db.FramePlaybackPackages.Remove(package);
            }
        }

        var existingCommands = await _db.FrameCommands
            .Where(c => c.DisplayDeviceId == device.Id)
            .ToListAsync(cancellationToken);

        foreach (var command in existingCommands)
        {
            if (command.CommandType == FrameCommandType.ClearFamilySharedContent
                && command.Status == FrameCommandStatus.Pending
                && command.PayloadJson == "{}")
            {
                continue;
            }

            _db.FrameCommands.Remove(command);
        }

        _db.DisplayDevices.Remove(device);
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> SendMemoryToDeviceAsync(
        Guid deviceId,
        SendMemoryToDeviceRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult.Fail("Unauthorized", 401);
        }

        var device = await _db.DisplayDevices
            .FirstOrDefaultAsync(d => d.Id == deviceId && d.FamilyId == ctx.Value.FamilyId, cancellationToken);

        if (device is null)
        {
            return ServiceResult.NotFound("Device not found");
        }

        var memory = await _db.Memories.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == request.MemoryId && m.FamilyId == ctx.Value.FamilyId, cancellationToken);

        if (memory is null)
        {
            return ServiceResult.NotFound("Memory not found");
        }

        var memoryPhotos = await _db.MemoryPhotos.AsNoTracking()
            .Where(mp => mp.MemoryId == request.MemoryId)
            .OrderBy(mp => mp.SortOrder)
            .Select(mp => mp.PhotoId)
            .ToListAsync(cancellationToken);

        var existingQueueCommand = await FindActivePlayMemoryCommandAsync(
            device.Id,
            request.MemoryId,
            cancellationToken);

        var alreadyInQueue = existingQueueCommand is not null
            || await HasActivePlaybackPackageForMemoryAsync(device.Id, request.MemoryId, cancellationToken);

        if (alreadyInQueue && !request.PlayNow)
        {
            return ServiceResult.Ok();
        }

        var payloadJson = await BuildPlayMemoryPayloadJsonAsync(
            ctx.Value.FamilyId,
            request,
            memory,
            memoryPhotos,
            cancellationToken);

        var commandToDeliver = new FrameCommand
        {
            DisplayDeviceId = device.Id,
            IssuedByUserId = ctx.Value.UserId,
            CommandType = FrameCommandType.PlayMemory,
            Status = FrameCommandStatus.Pending,
            PayloadJson = payloadJson
        };
        _db.FrameCommands.Add(commandToDeliver);

        if (request.PlayNow)
        {
            device.CurrentMemoryId = request.MemoryId;
        }

        device.Status = DisplayDeviceStatus.Online;
        device.LastSeenAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await DeliverFrameCommandsViaWebSocketAsync(device.Id, [commandToDeliver], cancellationToken);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> SendActivityToDeviceAsync(
        Guid deviceId,
        SendActivityToDeviceRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult.Fail("Unauthorized", 401);
        }

        if (string.IsNullOrWhiteSpace(request.ActivityId))
        {
            return ServiceResult.Fail("activityId is required", 400);
        }

        var device = await _db.DisplayDevices
            .FirstOrDefaultAsync(d => d.Id == deviceId && d.FamilyId == ctx.Value.FamilyId, cancellationToken);

        if (device is null)
        {
            return ServiceResult.NotFound("Device not found");
        }

        var activity = await ActivityAlbumAccess.ResolveAsync(
            _db,
            ctx.Value.FamilyId,
            request.ActivityId,
            cancellationToken);

        if (activity is null)
        {
            return ServiceResult.NotFound("Activity not found");
        }

        if (!await ActivityAlbumAccess.CanAccessAsync(_db, activity, ctx.Value.UserId, cancellationToken))
        {
            return ServiceResult.Fail("Forbidden", 403);
        }

        var activityExternalId = activity.ExternalId;
        var photoIds = await LoadActivityAlbumPhotoIdsAsync(activity.Id, cancellationToken);
        if (photoIds.Count == 0)
        {
            return ServiceResult.Fail("Activity has no photos", 400);
        }

        var existingQueueCommand = await FindActivePlayActivityCommandAsync(
            device.Id,
            activityExternalId,
            cancellationToken);

        if (existingQueueCommand is not null && !request.PlayNow)
        {
            return ServiceResult.Ok();
        }

        var payloadJson = await BuildPlayActivityPayloadJsonAsync(
            ctx.Value.FamilyId,
            request,
            activity,
            photoIds,
            cancellationToken);

        var commandToDeliver = new FrameCommand
        {
            DisplayDeviceId = device.Id,
            IssuedByUserId = ctx.Value.UserId,
            CommandType = FrameCommandType.PlayActivity,
            Status = FrameCommandStatus.Pending,
            PayloadJson = payloadJson
        };
        _db.FrameCommands.Add(commandToDeliver);

        device.Status = DisplayDeviceStatus.Online;
        device.LastSeenAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await DeliverFrameCommandsViaWebSocketAsync(device.Id, [commandToDeliver], cancellationToken);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> CancelPlaybackQueueCommandAsync(
        Guid deviceId,
        Guid commandId,
        CancellationToken cancellationToken = default)
    {
        var (ok, error) = await ResolveOwnedDeviceAsync(deviceId, cancellationToken);
        if (!ok)
        {
            return error!;
        }

        var removal = await FramePlaybackQueueSync.RemoveFromDeviceQueueAsync(
            _db,
            deviceId,
            commandId,
            memoryId: null,
            removePlaybackPackages: true,
            cancellationToken);

        return await CompleteQueueRemovalAsync(deviceId, removal, cancellationToken);
    }

    public async Task<ServiceResult> CancelPlaybackQueueByMemoryAsync(
        Guid deviceId,
        Guid memoryId,
        CancellationToken cancellationToken = default)
    {
        var (ok, error) = await ResolveOwnedDeviceAsync(deviceId, cancellationToken);
        if (!ok)
        {
            return error!;
        }

        var removal = await FramePlaybackQueueSync.RemoveFromDeviceQueueAsync(
            _db,
            deviceId,
            commandId: null,
            memoryId,
            removePlaybackPackages: true,
            cancellationToken);

        return await CompleteQueueRemovalAsync(deviceId, removal, cancellationToken);
    }

    public async Task<ServiceResult> CancelPlaybackQueueByActivityAsync(
        Guid deviceId,
        string activityId,
        CancellationToken cancellationToken = default)
    {
        var (ok, error) = await ResolveOwnedDeviceAsync(deviceId, cancellationToken);
        if (!ok)
        {
            return error!;
        }

        var removal = await FramePlaybackQueueSync.RemoveFromDeviceQueueByActivityAsync(
            _db,
            deviceId,
            activityId,
            cancellationToken);

        if (!removal.Success)
        {
            return ServiceResult.NotFound("Queue item not found");
        }

        return ServiceResult.Ok();
    }

    private async Task<ServiceResult> CompleteQueueRemovalAsync(
        Guid deviceId,
        FrameQueueRemovalResult removal,
        CancellationToken cancellationToken)
    {
        if (!removal.Success)
        {
            return removal.Equals(FrameQueueRemovalResult.Inactive)
                ? ServiceResult.Fail("Queue item is no longer active", 409)
                : ServiceResult.NotFound("Queue item not found");
        }

        await FramePlaybackQueueSync.NotifyRemoveQueueItemAsync(_webSocketHub, deviceId, removal, cancellationToken);
        return ServiceResult.Ok();
    }

    private async Task<(bool Ok, ServiceResult? Error)> ResolveOwnedDeviceAsync(
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return (false, ServiceResult.Fail("Unauthorized", 401));
        }

        var exists = await _db.DisplayDevices.AsNoTracking()
            .AnyAsync(d => d.Id == deviceId && d.FamilyId == ctx.Value.FamilyId, cancellationToken);

        if (!exists)
        {
            return (false, ServiceResult.NotFound("Device not found"));
        }

        return (true, null);
    }

    public async Task<ServiceResult<IReadOnlyList<DisplayFrameQueueItemDto>>> GetPlaybackQueueAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<IReadOnlyList<DisplayFrameQueueItemDto>>.Fail("Unauthorized", 401);
        }

        var device = await _db.DisplayDevices.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == deviceId && d.FamilyId == ctx.Value.FamilyId, cancellationToken);

        if (device is null)
        {
            return ServiceResult<IReadOnlyList<DisplayFrameQueueItemDto>>.NotFound("Device not found");
        }

        var commands = await _db.FrameCommands.AsNoTracking()
            .Where(c =>
                c.DisplayDeviceId == deviceId
                && (c.CommandType == FrameCommandType.PlayMemory
                    || c.CommandType == FrameCommandType.PlayActivity)
                && (c.Status == FrameCommandStatus.Pending || c.Status == FrameCommandStatus.Delivered))
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        var queue = new List<DisplayFrameQueueItemDto>();
        var seenMemoryIds = new HashSet<Guid>();
        var seenActivityIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var command in commands)
        {
            if (command.CommandType == FrameCommandType.PlayMemory)
            {
                if (!FramePlayMemoryPayload.TryParse(command.PayloadJson, out var memoryId, out var playNow, out var title)
                    || !memoryId.HasValue
                    || !seenMemoryIds.Add(memoryId.Value))
                {
                    continue;
                }

                queue.Add(new DisplayFrameQueueItemDto
                {
                    Id = command.Id,
                    Title = title ?? string.Empty,
                    MemoryId = memoryId,
                    CommandType = command.CommandType,
                    Status = command.Status,
                    PlayNow = playNow,
                    CreatedAt = command.CreatedAt
                });
                continue;
            }

            if (command.CommandType != FrameCommandType.PlayActivity)
            {
                continue;
            }

            if (!FramePlayActivityPayload.TryParse(command.PayloadJson, out var activityId, out var activityPlayNow, out var activityTitle)
                || string.IsNullOrEmpty(activityId)
                || !seenActivityIds.Add(activityId))
            {
                continue;
            }

            queue.Add(new DisplayFrameQueueItemDto
            {
                Id = command.Id,
                Title = activityTitle ?? string.Empty,
                ActivityId = activityId,
                CommandType = command.CommandType,
                Status = command.Status,
                PlayNow = activityPlayNow,
                CreatedAt = command.CreatedAt
            });
        }

        return ServiceResult<IReadOnlyList<DisplayFrameQueueItemDto>>.Ok(queue);
    }

    public Task<ServiceResult<string>> GenerateQrCodeAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(ServiceResult<string>.Ok(Guid.NewGuid().ToString()));

    public async Task<ServiceResult<DisplayDevicePairingStatusDto>> GetPairingStatusAsync(
        string qrCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(qrCode))
        {
            return ServiceResult<DisplayDevicePairingStatusDto>.Fail("qrCode is required", 400);
        }

        if (!TryNormalizePairingQrCode(qrCode, out var normalized, out var qrError))
        {
            return ServiceResult<DisplayDevicePairingStatusDto>.Fail(qrError!, 400);
        }

        var device = await _db.DisplayDevices.AsNoTracking()
            .FirstOrDefaultAsync(d => d.QrCode == normalized, cancellationToken);

        if (device is null)
        {
            return ServiceResult<DisplayDevicePairingStatusDto>.Ok(new DisplayDevicePairingStatusDto
            {
                IsBound = false,
                CanBind = _pairingSessions.IsActive(normalized)
            });
        }

        return ServiceResult<DisplayDevicePairingStatusDto>.Ok(new DisplayDevicePairingStatusDto
        {
            IsBound = true,
            CanBind = false,
            DeviceId = device.Id,
            Name = device.Name
        });
    }

    public Task<ServiceResult> RegisterPairingSessionAsync(
        string qrCode,
        CancellationToken cancellationToken = default)
    {
        if (!TryNormalizePairingQrCode(qrCode, out var normalized, out var qrError))
        {
            return Task.FromResult(ServiceResult.Fail(qrError!, 400));
        }

        _pairingSessions.Register(normalized, PairingSessionTtl);
        return Task.FromResult(ServiceResult.Ok());
    }

    private static bool TryNormalizePairingQrCode(string qrCode, out string normalized, out string? error)
    {
        normalized = string.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(qrCode))
        {
            error = "qrCode is required";
            return false;
        }

        var trimmed = qrCode.Trim();
        const string framePairPrefix = "memoressa://frame-pair/";
        if (trimmed.StartsWith(framePairPrefix, StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[framePairPrefix.Length..].Trim();
        }

        if (!Guid.TryParse(trimmed, out var guid))
        {
            error = "Invalid pairing code";
            return false;
        }

        normalized = guid.ToString();
        return true;
    }

    private async Task<FrameCommand?> FindActivePlayMemoryCommandAsync(
        Guid deviceId,
        Guid memoryId,
        CancellationToken cancellationToken)
    {
        var commands = await _db.FrameCommands
            .Where(c =>
                c.DisplayDeviceId == deviceId
                && c.CommandType == FrameCommandType.PlayMemory
                && (c.Status == FrameCommandStatus.Pending || c.Status == FrameCommandStatus.Delivered))
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        foreach (var command in commands)
        {
            if (FramePlayMemoryPayload.TryParse(command.PayloadJson, out var parsedMemoryId, out _, out _)
                && parsedMemoryId == memoryId)
            {
                return command;
            }
        }

        return null;
    }

    private async Task<bool> HasActivePlaybackPackageForMemoryAsync(
        Guid deviceId,
        Guid memoryId,
        CancellationToken cancellationToken)
    {
        var externalId = FramePlaybackQueueSync.RemotePackageExternalId(memoryId);
        var packages = await _db.FramePlaybackPackages.AsNoTracking()
            .Where(p => p.DisplayDeviceId == deviceId && p.IsActive)
            .Select(p => new { p.ExternalId, p.PackageJson })
            .ToListAsync(cancellationToken);

        return packages.Exists(p =>
            p.ExternalId == externalId
            || FramePlayMemoryPayload.PackageJsonContainsMemoryId(p.PackageJson, memoryId));
    }

    private async Task<List<Guid>> LoadActivityAlbumPhotoIdsAsync(
        Guid activityAlbumId,
        CancellationToken cancellationToken)
    {
        return await _db.ActivityAlbumPhotos.AsNoTracking()
            .Where(ap => ap.ActivityAlbumId == activityAlbumId)
            .OrderByDescending(ap => ap.SortOrder)
            .ThenByDescending(ap => ap.CreatedAt)
            .Take(SendActivityPhotosLimit)
            .Select(ap => ap.PhotoId)
            .ToListAsync(cancellationToken);
    }

    private async Task<FrameCommand?> FindActivePlayActivityCommandAsync(
        Guid deviceId,
        string activityExternalId,
        CancellationToken cancellationToken)
    {
        var commands = await _db.FrameCommands
            .Where(c =>
                c.DisplayDeviceId == deviceId
                && c.CommandType == FrameCommandType.PlayActivity
                && (c.Status == FrameCommandStatus.Pending || c.Status == FrameCommandStatus.Delivered))
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        foreach (var command in commands)
        {
            if (FramePlayActivityPayload.TryParse(command.PayloadJson, out var parsedId, out _, out _)
                && string.Equals(parsedId, activityExternalId, StringComparison.Ordinal))
            {
                return command;
            }
        }

        return null;
    }

    private async Task<string> BuildPlayActivityPayloadJsonAsync(
        Guid familyId,
        SendActivityToDeviceRequestDto request,
        ActivityAlbum activity,
        IReadOnlyList<Guid> photoIds,
        CancellationToken cancellationToken)
    {
        var items = await _aiOrchestrationService.GeneratePlaybackAsync(
            familyId,
            new PlaybackRequestDto
            {
                PhotoIds = photoIds,
                AiCurated = false,
                UrlPurpose = PhotoUrlPurpose.FramePlayback
            },
            cancellationToken);

        var memberIds = activity.FamilyMembers.Select(m => m.FamilyMemberId).ToList();
        var memberNames = memberIds.Count == 0
            ? []
            : await _db.FamilyMembers.AsNoTracking()
                .Where(m => memberIds.Contains(m.Id))
                .Select(m => m.Name)
                .ToListAsync(cancellationToken);

        var typeApi = ActivityAlbumMapping.TypeToApiString(activity.Type);

        return JsonSerializer.Serialize(new
        {
            activityId = activity.ExternalId,
            playNow = request.PlayNow,
            packageTitle = request.PackageTitle ?? activity.Title,
            activityAlbumType = typeApi,
            activityType = typeApi,
            activityLocation = activity.Location,
            startDate = activity.StartDate,
            endDate = activity.EndDate,
            familyMemberIds = memberIds.Select(id => id.ToString()).ToList(),
            memberNames,
            items
        });
    }

    private async Task<string> BuildPlayMemoryPayloadJsonAsync(
        Guid familyId,
        SendMemoryToDeviceRequestDto request,
        Memory memory,
        IReadOnlyList<Guid> memoryPhotos,
        CancellationToken cancellationToken)
    {
        var items = await _aiOrchestrationService.GeneratePlaybackAsync(
            familyId,
            new PlaybackRequestDto
            {
                PhotoIds = memoryPhotos,
                AiCurated = false,
                UrlPurpose = PhotoUrlPurpose.FramePlayback
            },
            cancellationToken);

        return JsonSerializer.Serialize(new
        {
            memoryId = request.MemoryId,
            playNow = request.PlayNow,
            packageTitle = request.PackageTitle ?? memory.Title,
            items
        });
    }

    private async Task DeliverFrameCommandsViaWebSocketAsync(
        Guid deviceId,
        IReadOnlyList<FrameCommand> commands,
        CancellationToken cancellationToken)
    {
        if (commands.Count == 0)
        {
            return;
        }

        foreach (var command in commands)
        {
            command.Status = FrameCommandStatus.Delivered;
            command.DeliveredAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);

        var payload = FrameDeviceWebSocketMessages.CommandsPayload(
            commands.Select(c => c.ToDto()).ToList());

        await _webSocketHub.SendJsonAsync(deviceId, payload, cancellationToken);
    }
}
