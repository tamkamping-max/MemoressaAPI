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

public class PhotoMemberReplaceTests
{
    [Fact]
    public async Task UpdatePhoto_With_Two_MemberIds_Including_Existing_Replaces_Rows()
    {
        var options = new DbContextOptionsBuilder<MemoressaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        await using var db = new MemoressaDbContext(options);

        var familyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var memberOneId = Guid.NewGuid();
        var memberTwoId = Guid.NewGuid();
        var photoId = Guid.NewGuid();

        db.Families.Add(new Family
        {
            Id = familyId,
            Name = "Test",
            OwnerUserId = userId,
        });
        db.UserAccounts.Add(new UserAccount
        {
            Id = userId,
            Email = "photo-member-test@memoressa.com",
        });
        db.FamilyMemberships.Add(new FamilyMembership
        {
            UserId = userId,
            FamilyId = familyId,
        });
        db.FamilyMembers.AddRange(
            new FamilyMember
            {
                Id = memberOneId,
                FamilyId = familyId,
                Name = "One",
                Generation = Generation.Self,
            },
            new FamilyMember
            {
                Id = memberTwoId,
                FamilyId = familyId,
                Name = "Two",
                Generation = Generation.Parent,
            });

        var photo = new Photo
        {
            Id = photoId,
            FamilyId = familyId,
            UploadedByUserId = userId,
            S3Key = "display.jpg",
            ContentType = "image/jpeg",
            Visibility = MemoryVisibility.SpecificMembers,
            PrivacyScope = UploadPrivacyScope.Custom,
        };
        db.Photos.Add(photo);
        db.PhotoMembers.Add(new PhotoMember
        {
            PhotoId = photoId,
            FamilyMemberId = memberOneId,
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
            new UpdatePhotoRequestDto
            {
                MemberIds = [memberOneId, memberTwoId],
                Visibility = MemoryVisibility.SpecificMembers,
            },
            CancellationToken.None);

        Assert.True(result.Success, result.Error);
        var memberLinks = await db.PhotoMembers.AsNoTracking()
            .Where(pm => pm.PhotoId == photoId)
            .Select(pm => pm.FamilyMemberId)
            .ToListAsync();
        Assert.Equal(2, memberLinks.Count);
        Assert.Contains(memberOneId, memberLinks);
        Assert.Contains(memberTwoId, memberLinks);
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
            Task.FromResult(new PhotoDto { Id = photo.Id });

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
