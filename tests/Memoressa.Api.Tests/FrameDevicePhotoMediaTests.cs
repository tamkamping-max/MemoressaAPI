using Memoressa.Application.Abstractions;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Application.Services;
using Memoressa.Domain.Entities;
using Memoressa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Api.Tests;

public class FrameDevicePhotoMediaTests
{
    [Fact]
    public async Task GetDevicePhotoMediaAsync_ReturnsPresignedUrlsForFamilyPhoto()
    {
        var familyId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var photoId = Guid.NewGuid();

        await using var db = CreateDb();
        db.Families.Add(new Family { Id = familyId, OwnerUserId = Guid.NewGuid(), Name = "F" });
        db.DisplayDevices.Add(new DisplayDevice
        {
            Id = deviceId,
            FamilyId = familyId,
            Name = "Frame",
            QrCode = Guid.NewGuid().ToString()
        });
        db.Photos.Add(new Photo
        {
            Id = photoId,
            FamilyId = familyId,
            UploadedByUserId = Guid.NewGuid(),
            S3Key = "uploads/k.jpg"
        });
        await db.SaveChangesAsync();

        var service = new FrameService(db, new AnonymousUser(), new StubPhotoUrls());

        var result = await service.GetDevicePhotoMediaAsync(deviceId, photoId);

        Assert.True(result.Success);
        Assert.Equal(photoId, result.Data!.PhotoId);
        Assert.Equal("remote:FramePlayback", result.Data.RemoteUrl);
        Assert.Equal("thumb:FramePlayback", result.Data.ThumbnailUrl);
    }

    [Fact]
    public async Task GetDevicePhotoMediaAsync_Returns404WhenPhotoWrongFamily()
    {
        var familyId = Guid.NewGuid();
        var otherFamilyId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var photoId = Guid.NewGuid();

        await using var db = CreateDb();
        db.Families.Add(new Family { Id = familyId, OwnerUserId = Guid.NewGuid(), Name = "F" });
        db.Families.Add(new Family { Id = otherFamilyId, OwnerUserId = Guid.NewGuid(), Name = "G" });
        db.DisplayDevices.Add(new DisplayDevice
        {
            Id = deviceId,
            FamilyId = familyId,
            Name = "Frame",
            QrCode = Guid.NewGuid().ToString()
        });
        db.Photos.Add(new Photo
        {
            Id = photoId,
            FamilyId = otherFamilyId,
            UploadedByUserId = Guid.NewGuid(),
            S3Key = "uploads/k.jpg"
        });
        await db.SaveChangesAsync();

        var service = new FrameService(db, new AnonymousUser(), new StubPhotoUrls());
        var result = await service.GetDevicePhotoMediaAsync(deviceId, photoId);

        Assert.Equal(404, result.StatusCode);
    }

    private static MemoressaDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<MemoressaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MemoressaDbContext(options);
    }

    private sealed class AnonymousUser : ICurrentUserService
    {
        public Guid? UserId => null;
        public Guid? FamilyId => null;
        public bool IsAuthenticated => false;
    }

    private sealed class StubPhotoUrls : IPhotoUrlResolver
    {
        public Task<PhotoDto> ToDtoAsync(
            Photo photo,
            PhotoUrlPurpose purpose = PhotoUrlPurpose.ApiResponse,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<PhotoDto>> ToDtosAsync(
            IEnumerable<Photo> photos,
            PhotoUrlPurpose purpose = PhotoUrlPurpose.ApiResponse,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<string?> GetPresignedUrlAsync(
            Photo photo,
            bool thumbnail = false,
            PhotoUrlPurpose purpose = PhotoUrlPurpose.ApiResponse,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(thumbnail ? $"thumb:{purpose}" : $"remote:{purpose}");
    }
}
