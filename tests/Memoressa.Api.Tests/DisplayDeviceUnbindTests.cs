using Memoressa.Application.Abstractions;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Application.Services;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;
using Memoressa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Api.Tests;

public class DisplayDeviceUnbindTests
{
    [Fact]
    public async Task UnbindAsync_RemovesFamilyPackagesAndPendingCommandsBeforeDeviceDelete()
    {
        var userId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        await using var db = CreateDb();
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
        db.FrameCommands.Add(new FrameCommand
        {
            DisplayDeviceId = deviceId,
            CommandType = FrameCommandType.PlayMemory,
            Status = FrameCommandStatus.Pending,
            PayloadJson = "{}"
        });
        db.FramePlaybackPackages.AddRange(
            new FramePlaybackPackage
            {
                DisplayDeviceId = deviceId,
                FamilyId = familyId,
                Title = "Family",
                PackageJson = "{}",
                IsActive = true
            },
            new FramePlaybackPackage
            {
                DisplayDeviceId = deviceId,
                FamilyId = familyId,
                Title = "Friend",
                PackageJson = "{\"isFriendShare\":true}",
                IsActive = true
            });
        await db.SaveChangesAsync();

        var service = new DisplayDeviceService(
            db,
            new FixedUser(userId, familyId),
            new StubAiOrchestration(),
            new StubPairingStore(),
            new NoOpFrameDeviceWebSocketHub());

        var result = await service.UnbindDeviceAsync(deviceId);

        Assert.True(result.Success);
        Assert.False(await db.DisplayDevices.AnyAsync(d => d.Id == deviceId));
        Assert.False(await db.FramePlaybackPackages.AnyAsync(p => p.DisplayDeviceId == deviceId));
        Assert.False(await db.FrameCommands.AnyAsync(c => c.DisplayDeviceId == deviceId));
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

    private sealed class StubPairingStore : IDisplayDevicePairingSessionStore
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
