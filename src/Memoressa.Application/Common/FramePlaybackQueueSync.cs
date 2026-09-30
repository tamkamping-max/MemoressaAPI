using Memoressa.Application.Abstractions;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Application.Common;

public static class FramePlaybackQueueSync
{
    public static string RemotePackageExternalId(Guid memoryId) => $"remote_pkg_{memoryId}";

    public static async Task<FrameQueueRemovalResult> RemoveFromDeviceQueueAsync(
        IMemoressaDbContext db,
        Guid deviceId,
        Guid? commandId,
        Guid? memoryId,
        bool removePlaybackPackages,
        CancellationToken cancellationToken)
    {
        if (!commandId.HasValue && !memoryId.HasValue)
        {
            return FrameQueueRemovalResult.NotFound;
        }

        var device = await db.DisplayDevices.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == deviceId, cancellationToken);

        if (device is null)
        {
            return FrameQueueRemovalResult.NotFound;
        }

        var resolvedMemoryId = memoryId;
        Guid? resolvedCommandId = commandId;
        var removed = false;

        if (commandId.HasValue)
        {
            var byId = await db.FrameCommands.FirstOrDefaultAsync(
                c => c.Id == commandId.Value
                     && c.DisplayDeviceId == deviceId
                     && c.CommandType == FrameCommandType.PlayMemory,
                cancellationToken);

            if (byId is null)
            {
                return FrameQueueRemovalResult.NotFound;
            }

            if (byId.Status is not (FrameCommandStatus.Pending or FrameCommandStatus.Delivered))
            {
                return FrameQueueRemovalResult.Inactive;
            }

            if (FramePlayMemoryPayload.TryParse(byId.PayloadJson, out var mid, out _, out _)
                && mid.HasValue)
            {
                resolvedMemoryId ??= mid;
            }

            resolvedCommandId = byId.Id;
            db.FrameCommands.Remove(byId);
            removed = true;
        }

        if (resolvedMemoryId.HasValue)
        {
            var activeCommands = await db.FrameCommands
                .Where(c =>
                    c.DisplayDeviceId == deviceId
                    && c.CommandType == FrameCommandType.PlayMemory
                    && (c.Status == FrameCommandStatus.Pending || c.Status == FrameCommandStatus.Delivered))
                .ToListAsync(cancellationToken);

            foreach (var command in activeCommands)
            {
                if (!FramePlayMemoryPayload.TryParse(command.PayloadJson, out var mid, out _, out _)
                    || mid != resolvedMemoryId.Value)
                {
                    continue;
                }

                resolvedCommandId ??= command.Id;
                db.FrameCommands.Remove(command);
                removed = true;
            }
        }

        Guid? packageId = null;
        if (removePlaybackPackages && resolvedMemoryId.HasValue)
        {
            var externalId = RemotePackageExternalId(resolvedMemoryId.Value);
            var packages = await db.FramePlaybackPackages
                .Where(p => p.DisplayDeviceId == deviceId)
                .ToListAsync(cancellationToken);

            foreach (var package in packages)
            {
                if (package.ExternalId == externalId
                    || FramePlayMemoryPayload.PackageJsonContainsMemoryId(package.PackageJson, resolvedMemoryId.Value))
                {
                    packageId ??= package.Id;
                    db.FramePlaybackPackages.Remove(package);
                    removed = true;
                }
            }
        }

        if (!removed)
        {
            return FrameQueueRemovalResult.NotFound;
        }

        await db.SaveChangesAsync(cancellationToken);

        return new FrameQueueRemovalResult(
            true,
            resolvedCommandId,
            resolvedMemoryId,
            packageId);
    }

    public static async Task<FrameQueueRemovalResult> RemovePlaybackPackageByIdAsync(
        IMemoressaDbContext db,
        Guid deviceId,
        Guid packageId,
        CancellationToken cancellationToken)
    {
        var device = await db.DisplayDevices.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == deviceId, cancellationToken);

        if (device is null)
        {
            return FrameQueueRemovalResult.NotFound;
        }

        var package = await db.FramePlaybackPackages
            .FirstOrDefaultAsync(p => p.Id == packageId && p.DisplayDeviceId == deviceId, cancellationToken);

        if (package is null)
        {
            return FrameQueueRemovalResult.NotFound;
        }

        var memoryId = TryResolveMemoryIdFromPackage(package);
        db.FramePlaybackPackages.Remove(package);

        Guid? commandId = null;
        if (memoryId.HasValue)
        {
            var activeCommands = await db.FrameCommands
                .Where(c =>
                    c.DisplayDeviceId == deviceId
                    && c.CommandType == FrameCommandType.PlayMemory
                    && (c.Status == FrameCommandStatus.Pending || c.Status == FrameCommandStatus.Delivered))
                .ToListAsync(cancellationToken);

            foreach (var command in activeCommands)
            {
                if (!FramePlayMemoryPayload.TryParse(command.PayloadJson, out var mid, out _, out _)
                    || mid != memoryId.Value)
                {
                    continue;
                }

                commandId ??= command.Id;
                db.FrameCommands.Remove(command);
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        return new FrameQueueRemovalResult(true, commandId, memoryId, packageId);
    }

    public static Guid? TryResolveMemoryIdFromPackage(FramePlaybackPackage package)
    {
        if (!string.IsNullOrEmpty(package.ExternalId))
        {
            const string prefix = "remote_pkg_";
            if (package.ExternalId.StartsWith(prefix, StringComparison.Ordinal)
                && Guid.TryParse(package.ExternalId.AsSpan(prefix.Length), out var fromExternal))
            {
                return fromExternal;
            }
        }

        if (string.IsNullOrWhiteSpace(package.PackageJson))
        {
            return null;
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(package.PackageJson);
            if (!doc.RootElement.TryGetProperty("memoryId", out var el))
            {
                return null;
            }

            if (el.ValueKind == System.Text.Json.JsonValueKind.String
                && Guid.TryParse(el.GetString(), out var parsed))
            {
                return parsed;
            }

            return el.TryGetGuid(out var guid) ? guid : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    public static Task NotifyRemoveQueueItemAsync(
        IFrameDeviceWebSocketHub hub,
        Guid deviceId,
        FrameQueueRemovalResult result,
        CancellationToken cancellationToken)
    {
        if (!result.Success || !result.MemoryId.HasValue)
        {
            return Task.CompletedTask;
        }

        return hub.SendJsonAsync(
            deviceId,
            FrameDeviceWebSocketMessages.RemoveQueueItemPayload(
                result.CommandId,
                result.MemoryId.Value,
                result.PackageId),
            cancellationToken);
    }
}

public readonly record struct FrameQueueRemovalResult(
    bool Success,
    Guid? CommandId,
    Guid? MemoryId,
    Guid? PackageId)
{
    public static FrameQueueRemovalResult NotFound => new(false, null, null, null);

    public static FrameQueueRemovalResult Inactive => new(false, null, null, null);
}
