using Memoressa.Application.Common;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Application.Services;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;
using Memoressa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Memoressa.Api.Tests;

public class PhotoFriendReplaceTests
{
    [Fact]
    public async Task UpdatePhoto_With_FriendIds_Replaces_PhotoFriends()
    {
        var options = new DbContextOptionsBuilder<MemoressaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        await using var db = new MemoressaDbContext(options);

        var familyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var friendOneId = Guid.NewGuid();
        var friendTwoId = Guid.NewGuid();
        var photoId = Guid.NewGuid();

        db.Families.Add(new Family { Id = familyId, Name = "Test", OwnerUserId = userId });
        db.UserAccounts.Add(new UserAccount { Id = userId, Email = "u@test.com" });
        db.FamilyMemberships.Add(new FamilyMembership { UserId = userId, FamilyId = familyId });
        db.Friends.AddRange(
            new Friend { Id = friendOneId, OwnerUserId = userId, Name = "F1" },
            new Friend { Id = friendTwoId, OwnerUserId = userId, Name = "F2" });

        var photo = new Photo
        {
            Id = photoId,
            FamilyId = familyId,
            UploadedByUserId = userId,
            S3Key = "display.jpg",
            ContentType = "image/jpeg",
        };
        db.Photos.Add(photo);
        db.PhotoFriends.Add(new PhotoFriend { PhotoId = photoId, FriendId = friendOneId });
        await db.SaveChangesAsync();

        var service = new PhotoService(
            db,
            new StubCurrentUser(userId),
            new StubPhotoUrlResolver(),
            new StubS3Storage(),
            Options.Create(new MediaStorageSettings()),
            new StubPhotoAlbumService());

        var result = await service.UpdatePhotoAsync(
            photoId,
            new UpdatePhotoRequestDto { FriendIds = [friendOneId, friendTwoId] },
            CancellationToken.None);

        Assert.True(result.Success, result.Error);
        var links = await db.PhotoFriends.AsNoTracking()
            .Where(pf => pf.PhotoId == photoId)
            .Select(pf => pf.FriendId)
            .ToListAsync();
        Assert.Equal(2, links.Count);
        Assert.Contains(friendOneId, links);
        Assert.Contains(friendTwoId, links);
    }

    [Fact]
    public async Task UpdatePhoto_With_Invalid_FriendId_Returns403()
    {
        var options = new DbContextOptionsBuilder<MemoressaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        await using var db = new MemoressaDbContext(options);

        var familyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var photoId = Guid.NewGuid();
        var strangerFriendId = Guid.NewGuid();

        db.Families.Add(new Family { Id = familyId, Name = "Test", OwnerUserId = userId });
        db.UserAccounts.Add(new UserAccount { Id = userId, Email = "u@test.com" });
        db.FamilyMemberships.Add(new FamilyMembership { UserId = userId, FamilyId = familyId });
        db.Friends.Add(new Friend
        {
            Id = strangerFriendId,
            OwnerUserId = Guid.NewGuid(),
            Name = "NotMine",
        });

        db.Photos.Add(new Photo
        {
            Id = photoId,
            FamilyId = familyId,
            UploadedByUserId = userId,
            S3Key = "k.jpg",
            ContentType = "image/jpeg",
        });
        await db.SaveChangesAsync();

        var service = new PhotoService(
            db,
            new StubCurrentUser(userId),
            new StubPhotoUrlResolver(),
            new StubS3Storage(),
            Options.Create(new MediaStorageSettings()),
            new StubPhotoAlbumService());

        var result = await service.UpdatePhotoAsync(
            photoId,
            new UpdatePhotoRequestDto { FriendIds = [strangerFriendId] },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(403, result.StatusCode);
    }

    private sealed class StubCurrentUser(Guid userId) : ICurrentUserService
    {
        public Guid? UserId => userId;
        public Guid? FamilyId => null;
        public bool IsAuthenticated => true;
    }

    private sealed class StubPhotoUrlResolver : IPhotoUrlResolver
    {
        public Task<PhotoDto> ToDtoAsync(
            Photo photo,
            PhotoUrlPurpose purpose = PhotoUrlPurpose.ApiResponse,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PhotoDto { Id = photo.Id, FriendIds = photo.PhotoFriends.Select(f => f.FriendId).ToList() });

        public Task<IReadOnlyList<PhotoDto>> ToDtosAsync(
            IEnumerable<Photo> photos,
            PhotoUrlPurpose purpose = PhotoUrlPurpose.ApiResponse,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PhotoDto>>(photos.Select(p => new PhotoDto { Id = p.Id }).ToList());

        public Task<string?> GetPresignedUrlAsync(
            Photo photo,
            bool thumbnail = false,
            PhotoUrlPurpose purpose = PhotoUrlPurpose.ApiResponse,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }

    private sealed class StubS3Storage : IS3StorageService
    {
        public string BuildObjectKey(Guid familyId, Guid userId, string fileName) => fileName;
        public PhotoUploadKeys.VariantKeys BuildPhotoUploadKeys(
            Guid familyId,
            Guid userId,
            string compressedFileName,
            string originalFileName,
            string? livePhotoVideoFileName) =>
            throw new NotImplementedException();
        public Task<string> GetPresignedPutUrlAsync(
            string s3Key,
            string contentType,
            TimeSpan expiry,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task<string> GetPresignedGetUrlAsync(
            string s3Key,
            TimeSpan expiry,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task DeleteObjectAsync(string s3Key, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task<bool> ObjectExistsAsync(string s3Key, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task<long?> GetObjectSizeBytesAsync(string s3Key, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task<byte[]> GetObjectBytesAsync(string s3Key, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task PutObjectAsync(
            string s3Key,
            byte[] bytes,
            string contentType,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public string BuildThumbnailKey(string originalS3Key) => originalS3Key;
    }

    private sealed class StubPhotoAlbumService : IPhotoAlbumService
    {
        public Task<ServiceResult<PhotoAlbumDto>> CreateOrFindAsync(
            CreatePhotoAlbumRequestDto request,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task<ServiceResult<PhotoAlbumDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task<ServiceResult<PhotoAlbumListPageDto>> ListCardsAsync(
            int? limit = null,
            string? cursor = null,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task<ServiceResult<PhotoAlbumDto>> UpdateAsync(
            Guid id,
            UpdatePhotoAlbumRequestDto request,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task<ServiceResult<PhotoAlbumDto>> PatchPhotosAsync(
            Guid id,
            PatchPhotoAlbumPhotosRequestDto request,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task<ServiceResult<PhotoAlbumDto>> UnlinkPhotosAsync(
            Guid id,
            UnlinkPhotoAlbumPhotosRequestDto request,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task<ServiceResult<IReadOnlyList<PhotoAlbumCommentDto>>> GetCommentsAsync(
            Guid albumId,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task<ServiceResult<PhotoAlbumCommentDto>> AddCommentAsync(
            Guid albumId,
            AddPhotoAlbumCommentRequestDto request,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task<ServiceResult> DeleteCommentAsync(
            Guid albumId,
            Guid commentId,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task<(Guid AlbumId, IReadOnlyList<string> UserTags, string? Description)?> TryGetPrimaryAlbumForPhotoAsync(
            Guid photoId,
            Guid familyId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<(Guid AlbumId, IReadOnlyList<string> UserTags, string? Description)?>(null);
    }
}
