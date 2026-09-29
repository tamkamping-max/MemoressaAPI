using System.Text.Json;
using Memoressa.Application.Abstractions;
using Memoressa.Application.Interfaces;
using Memoressa.Application.Common;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Application.Services;

public class DisplayDeviceService : IDisplayDeviceService
{
    private static readonly TimeSpan PairingSessionTtl = TimeSpan.FromMinutes(10);

    private readonly IMemoressaDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAiOrchestrationService _aiOrchestrationService;
    private readonly IDisplayDevicePairingSessionStore _pairingSessions;

    public DisplayDeviceService(
        IMemoressaDbContext db,
        ICurrentUserService currentUser,
        IAiOrchestrationService aiOrchestrationService,
        IDisplayDevicePairingSessionStore pairingSessions)
    {
        _db = db;
        _currentUser = currentUser;
        _aiOrchestrationService = aiOrchestrationService;
        _pairingSessions = pairingSessions;
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

        var items = await _aiOrchestrationService.GeneratePlaybackAsync(
            ctx.Value.FamilyId,
            new PlaybackRequestDto
            {
                PhotoIds = memoryPhotos,
                AiCurated = false,
                UrlPurpose = PhotoUrlPurpose.FramePlayback
            },
            cancellationToken);

        var payload = JsonSerializer.Serialize(new
        {
            memoryId = request.MemoryId,
            playNow = request.PlayNow,
            packageTitle = request.PackageTitle ?? memory.Title,
            items
        });

        _db.FrameCommands.Add(new FrameCommand
        {
            DisplayDeviceId = device.Id,
            IssuedByUserId = ctx.Value.UserId,
            CommandType = FrameCommandType.PlayMemory,
            Status = FrameCommandStatus.Pending,
            PayloadJson = payload
        });

        if (request.PlayNow)
        {
            device.CurrentMemoryId = request.MemoryId;
        }

        device.Status = DisplayDeviceStatus.Online;
        device.LastSeenAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok();
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
                && c.CommandType == FrameCommandType.PlayMemory
                && (c.Status == FrameCommandStatus.Pending || c.Status == FrameCommandStatus.Delivered))
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        var queue = new List<DisplayFrameQueueItemDto>(commands.Count);
        foreach (var command in commands)
        {
            if (!TryParsePlayMemoryPayload(command.PayloadJson, out var memoryId, out var playNow, out var title))
            {
                continue;
            }

            if (playNow)
            {
                continue;
            }

            queue.Add(new DisplayFrameQueueItemDto
            {
                Id = command.Id,
                Title = title ?? string.Empty,
                MemoryId = memoryId,
                Status = command.Status,
                PlayNow = playNow,
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

    private static bool TryParsePlayMemoryPayload(
        string payloadJson,
        out Guid? memoryId,
        out bool playNow,
        out string? title)
    {
        memoryId = null;
        playNow = false;
        title = null;

        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!root.TryGetProperty("memoryId", out var memoryIdElement)
                || memoryIdElement.ValueKind != JsonValueKind.String
                || !Guid.TryParse(memoryIdElement.GetString(), out var parsedMemoryId))
            {
                return false;
            }

            memoryId = parsedMemoryId;

            if (root.TryGetProperty("playNow", out var playNowElement)
                && (playNowElement.ValueKind == JsonValueKind.True || playNowElement.ValueKind == JsonValueKind.False))
            {
                playNow = playNowElement.GetBoolean();
            }

            if (root.TryGetProperty("packageTitle", out var titleElement)
                && titleElement.ValueKind == JsonValueKind.String)
            {
                title = titleElement.GetString();
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
