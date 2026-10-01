using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Application.Services;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;
using Memoressa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Memoressa.Api.Tests;

public class DisplayDevicePlaybackQueueTests
{
    [Fact]
    public async Task GetPlaybackQueueAsync_IncludesPlayNowAndDedupesByMemoryId()
    {
        var userId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var memoryId = Guid.NewGuid();

        await using var db = CreateDb();
        SeedDevice(db, userId, familyId, deviceId);

        db.FrameCommands.AddRange(
            new FrameCommand
            {
                DisplayDeviceId = deviceId,
                CommandType = FrameCommandType.PlayMemory,
                Status = FrameCommandStatus.Pending,
                PayloadJson = JsonSerializer.Serialize(new
                {
                    memoryId,
                    playNow = false,
                    packageTitle = "First"
                }),
                CreatedAt = DateTime.UtcNow.AddMinutes(-2)
            },
            new FrameCommand
            {
                DisplayDeviceId = deviceId,
                CommandType = FrameCommandType.PlayMemory,
                Status = FrameCommandStatus.Pending,
                PayloadJson = JsonSerializer.Serialize(new
                {
                    memoryId,
                    playNow = true,
                    packageTitle = "Duplicate row"
                }),
                CreatedAt = DateTime.UtcNow.AddMinutes(-1)
            },
            new FrameCommand
            {
                DisplayDeviceId = deviceId,
                CommandType = FrameCommandType.PlayMemory,
                Status = FrameCommandStatus.Acknowledged,
                PayloadJson = JsonSerializer.Serialize(new { memoryId = Guid.NewGuid(), playNow = false }),
                CreatedAt = DateTime.UtcNow
            });

        await db.SaveChangesAsync();

        var service = CreateService(db, userId, familyId);
        var result = await service.GetPlaybackQueueAsync(deviceId);

        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Equal(memoryId, result.Data![0].MemoryId);
        Assert.Equal("First", result.Data[0].Title);
        Assert.False(result.Data[0].PlayNow);
    }

    [Fact]
    public async Task SendActivityToDeviceAsync_WritesPlayActivityCommandWithPayload()
    {
        var userId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var externalId = "act_test_send";
        var photoId = Guid.NewGuid();

        await using var db = CreateDb();
        SeedDevice(db, userId, familyId, deviceId);
        db.ActivityAlbums.Add(new ActivityAlbum
        {
            Id = activityId,
            FamilyId = familyId,
            ExternalId = externalId,
            Title = "Trip",
            Type = ActivityAlbumType.Travel,
            Status = ActivityAlbumStatus.InProgress,
            StartDate = new DateOnly(2026, 3, 1),
            Location = "Tokyo",
            CreatorUserId = userId
        });
        db.Photos.Add(new Photo
        {
            Id = photoId,
            FamilyId = familyId,
            UploadedByUserId = userId,
            S3Key = "photos/test.jpg"
        });
        db.ActivityAlbumPhotos.Add(new ActivityAlbumPhoto
        {
            ActivityAlbumId = activityId,
            PhotoId = photoId,
            SortOrder = 1
        });
        await db.SaveChangesAsync();

        var hub = new NoOpFrameDeviceWebSocketHub();
        var service = CreateService(db, userId, familyId, hub);
        var result = await service.SendActivityToDeviceAsync(
            deviceId,
            new SendActivityToDeviceRequestDto
            {
                ActivityId = externalId,
                PlayNow = true,
                PackageTitle = "Our trip"
            });

        Assert.True(result.Success);
        var command = await db.FrameCommands.SingleAsync(c => c.DisplayDeviceId == deviceId);
        Assert.Equal(FrameCommandType.PlayActivity, command.CommandType);
        Assert.Contains(externalId, command.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("activityAlbumType", command.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("travel", command.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("Tokyo", command.PayloadJson, StringComparison.Ordinal);
        Assert.Single(hub.SentMessages);
    }

    [Fact]
    public async Task GetPlaybackQueueAsync_IncludesPlayActivityDedupedByActivityId()
    {
        var userId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        const string activityId = "act_queue";

        await using var db = CreateDb();
        SeedDevice(db, userId, familyId, deviceId);
        db.FrameCommands.AddRange(
            new FrameCommand
            {
                DisplayDeviceId = deviceId,
                CommandType = FrameCommandType.PlayActivity,
                Status = FrameCommandStatus.Pending,
                PayloadJson = JsonSerializer.Serialize(new
                {
                    activityId,
                    playNow = false,
                    packageTitle = "First"
                }),
                CreatedAt = DateTime.UtcNow.AddMinutes(-2)
            },
            new FrameCommand
            {
                DisplayDeviceId = deviceId,
                CommandType = FrameCommandType.PlayActivity,
                Status = FrameCommandStatus.Pending,
                PayloadJson = JsonSerializer.Serialize(new
                {
                    activityId,
                    playNow = true,
                    packageTitle = "Duplicate"
                }),
                CreatedAt = DateTime.UtcNow.AddMinutes(-1)
            });
        await db.SaveChangesAsync();

        var service = CreateService(db, userId, familyId);
        var result = await service.GetPlaybackQueueAsync(deviceId);

        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Equal(activityId, result.Data![0].ActivityId);
        Assert.Equal(FrameCommandType.PlayActivity, result.Data[0].CommandType);
        Assert.Equal("First", result.Data[0].Title);
        Assert.False(result.Data[0].PlayNow);
    }

    [Fact]
    public async Task SendMemoryToDeviceAsync_DoesNotDuplicateWhenAlreadyQueued()
    {
        var userId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var memoryId = Guid.NewGuid();

        await using var db = CreateDb();
        SeedDevice(db, userId, familyId, deviceId);
        db.Memories.Add(new Memory
        {
            Id = memoryId,
            FamilyId = familyId,
            Title = "Album",
            CreatedByUserId = userId
        });
        db.FrameCommands.Add(new FrameCommand
        {
            DisplayDeviceId = deviceId,
            CommandType = FrameCommandType.PlayMemory,
            Status = FrameCommandStatus.Pending,
            PayloadJson = JsonSerializer.Serialize(new { memoryId, playNow = false, packageTitle = "Album" })
        });
        await db.SaveChangesAsync();

        var hub = new NoOpFrameDeviceWebSocketHub();
        var service = CreateService(db, userId, familyId, hub);
        var result = await service.SendMemoryToDeviceAsync(
            deviceId,
            new SendMemoryToDeviceRequestDto { MemoryId = memoryId, PlayNow = false });

        Assert.True(result.Success);
        Assert.Equal(1, await db.FrameCommands.CountAsync(c => c.DisplayDeviceId == deviceId));
        Assert.Empty(hub.SentMessages);
    }

    [Fact]
    public async Task SendMemoryToDeviceAsync_PlayNowOnExisting_InsertsNewCommandAndPushesWebSocket()
    {
        var userId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var memoryId = Guid.NewGuid();

        await using var db = CreateDb();
        SeedDevice(db, userId, familyId, deviceId);
        db.Memories.Add(new Memory
        {
            Id = memoryId,
            FamilyId = familyId,
            Title = "Album",
            CreatedByUserId = userId
        });
        var existing = new FrameCommand
        {
            DisplayDeviceId = deviceId,
            CommandType = FrameCommandType.PlayMemory,
            Status = FrameCommandStatus.Delivered,
            DeliveredAt = DateTime.UtcNow.AddMinutes(-1),
            PayloadJson = JsonSerializer.Serialize(new { memoryId, playNow = false, packageTitle = "Album" })
        };
        db.FrameCommands.Add(existing);
        await db.SaveChangesAsync();

        var hub = new NoOpFrameDeviceWebSocketHub();
        var service = CreateService(db, userId, familyId, hub);
        var result = await service.SendMemoryToDeviceAsync(
            deviceId,
            new SendMemoryToDeviceRequestDto { MemoryId = memoryId, PlayNow = true });

        Assert.True(result.Success);
        Assert.Equal(2, await db.FrameCommands.CountAsync(c => c.DisplayDeviceId == deviceId));
        Assert.Single(hub.SentMessages);
        Assert.Contains("commands", hub.SentMessages[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("playNow", hub.SentMessages[0], StringComparison.OrdinalIgnoreCase);

        var reloaded = await db.FrameCommands.SingleAsync(c => c.Id == existing.Id);
        Assert.Equal(FrameCommandStatus.Delivered, reloaded.Status);
        Assert.Contains("\"playNow\":false", reloaded.PayloadJson, StringComparison.OrdinalIgnoreCase);

        var playNowCommand = await db.FrameCommands.SingleAsync(c => c.Id != existing.Id);
        Assert.Equal(FrameCommandStatus.Delivered, playNowCommand.Status);
        Assert.Contains("\"playNow\":true", playNowCommand.PayloadJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendMemoryToDeviceAsync_PlayNowWhenOnlyPackageQueued_InsertsPlayNowCommandAndPushesWebSocket()
    {
        var userId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var memoryId = Guid.NewGuid();

        await using var db = CreateDb();
        SeedDevice(db, userId, familyId, deviceId);
        db.Memories.Add(new Memory
        {
            Id = memoryId,
            FamilyId = familyId,
            Title = "Album",
            CreatedByUserId = userId
        });
        db.FramePlaybackPackages.Add(new FramePlaybackPackage
        {
            DisplayDeviceId = deviceId,
            FamilyId = familyId,
            ExternalId = $"remote_pkg_{memoryId}",
            Title = "Album",
            PackageJson = JsonSerializer.Serialize(new { memoryId }),
            IsActive = true
        });
        await db.SaveChangesAsync();

        var hub = new NoOpFrameDeviceWebSocketHub();
        var service = CreateService(db, userId, familyId, hub);
        var result = await service.SendMemoryToDeviceAsync(
            deviceId,
            new SendMemoryToDeviceRequestDto { MemoryId = memoryId, PlayNow = true });

        Assert.True(result.Success);
        Assert.Equal(1, await db.FrameCommands.CountAsync());
        Assert.Single(hub.SentMessages);
        Assert.Contains("commands", hub.SentMessages[0], StringComparison.OrdinalIgnoreCase);

        var command = await db.FrameCommands.SingleAsync();
        Assert.Contains("\"playNow\":true", command.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(FrameCommandStatus.Delivered, command.Status);
    }

    [Fact]
    public async Task CancelPlaybackQueueCommandAsync_RemovesRowAndSendsRemoveQueueItem()
    {
        var userId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var memoryId = Guid.NewGuid();
        var commandId = Guid.NewGuid();

        await using var db = CreateDb();
        SeedDevice(db, userId, familyId, deviceId);
        db.FrameCommands.Add(new FrameCommand
        {
            Id = commandId,
            DisplayDeviceId = deviceId,
            CommandType = FrameCommandType.PlayMemory,
            Status = FrameCommandStatus.Pending,
            PayloadJson = JsonSerializer.Serialize(new { memoryId, playNow = false, packageTitle = "X" })
        });
        await db.SaveChangesAsync();

        var hub = new NoOpFrameDeviceWebSocketHub();
        var service = CreateService(db, userId, familyId, hub);
        var result = await service.CancelPlaybackQueueCommandAsync(deviceId, commandId);

        Assert.True(result.Success);
        Assert.False(await db.FrameCommands.AnyAsync(c => c.Id == commandId));
        Assert.Single(hub.SentMessages);
        Assert.Contains("remove_queue_item", hub.SentMessages[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains(memoryId.ToString(), hub.SentMessages[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CancelPlaybackQueueByMemoryAsync_RemovesMatchingCommands()
    {
        var userId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var memoryId = Guid.NewGuid();

        await using var db = CreateDb();
        SeedDevice(db, userId, familyId, deviceId);
        db.FrameCommands.Add(new FrameCommand
        {
            DisplayDeviceId = deviceId,
            CommandType = FrameCommandType.PlayMemory,
            Status = FrameCommandStatus.Pending,
            PayloadJson = JsonSerializer.Serialize(new { memoryId, playNow = false, packageTitle = "X" })
        });
        await db.SaveChangesAsync();

        var hub = new NoOpFrameDeviceWebSocketHub();
        var service = CreateService(db, userId, familyId, hub);
        var result = await service.CancelPlaybackQueueByMemoryAsync(deviceId, memoryId);

        Assert.True(result.Success);
        Assert.False(await db.FrameCommands.AnyAsync(c => c.DisplayDeviceId == deviceId));
        Assert.Single(hub.SentMessages);
    }

    private static DisplayDeviceService CreateService(
        MemoressaDbContext db,
        Guid userId,
        Guid familyId,
        NoOpFrameDeviceWebSocketHub? hub = null) =>
        new(
            db,
            new FixedUser(userId, familyId),
            new StubAiOrchestration(),
            new StubPairingStore(),
            hub ?? new NoOpFrameDeviceWebSocketHub());

    private static void SeedDevice(MemoressaDbContext db, Guid userId, Guid familyId, Guid deviceId)
    {
        db.UserAccounts.Add(new UserAccount { Id = userId, Email = "u@test.com", PasswordHash = "x" });
        db.Families.Add(new Family { Id = familyId, OwnerUserId = userId, Name = "F" });
        db.FamilyMemberships.Add(new FamilyMembership { FamilyId = familyId, UserId = userId, Role = "owner" });
        db.DisplayDevices.Add(new DisplayDevice
        {
            Id = deviceId,
            FamilyId = familyId,
            BoundByUserId = userId,
            Name = "Frame",
            QrCode = Guid.NewGuid().ToString()
        });
    }

    private static MemoressaDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<MemoressaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MemoressaDbContext(options);
    }

    private sealed class FixedUser(Guid userId, Guid familyId) : ICurrentUserService
    {
        public Guid? UserId => userId;
        public Guid? FamilyId => familyId;
        public bool IsAuthenticated => true;
    }

    private sealed class StubPairingStore : Memoressa.Application.Abstractions.IDisplayDevicePairingSessionStore
    {
        public void Register(string qrCode, TimeSpan ttl) { }
        public bool IsActive(string qrCode) => false;
        public void Remove(string qrCode) { }
    }

    private sealed class StubAiOrchestration : IAiOrchestrationService
    {
        public Task<AiAnalysisResultDto> AnalyzePhotosAsync(
            Guid userId,
            Guid familyId,
            IReadOnlyList<Guid> photoIds,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<SearchResultDto>> SearchMemoriesAsync(
            Guid familyId,
            string query,
            CancellationToken cancellationToken = default,
            IReadOnlyList<string>? additionalSearchTerms = null) =>
            throw new NotImplementedException();

        public Task<int> CountVisiblePhotosAsync(Guid familyId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<int> CountVisiblePhotosSinceAsync(
            Guid familyId,
            DateTime sinceUtc,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<SearchResultDto>> ListVisiblePhotosForAgentAsync(
            Guid familyId,
            int limit,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<SearchResultDto>> ListVisiblePhotosSinceAsync(
            Guid familyId,
            DateTime sinceUtc,
            int limit,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<PlaybackItemDto>> GeneratePlaybackAsync(
            Guid familyId,
            PlaybackRequestDto request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PlaybackItemDto>>([]);

        public Task<MemoryDto> CreateAiMemoryAsync(
            Guid userId,
            Guid familyId,
            IReadOnlyList<Guid> photoIds,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task ProcessVisionBatchAsync(Guid jobId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }
}
