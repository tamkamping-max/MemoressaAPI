using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;

namespace Memoressa.Application.DTOs;

public static class EntityMapping
{
    public static UserDto ToDto(this UserAccount user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        Nickname = user.Nickname,
        AvatarUrl = user.AvatarUrl,
        Generation = user.Generation,
        SelfFamilyMemberId = user.SelfFamilyMemberId,
        CreatedAt = user.CreatedAt
    };

    public static PhotoDto ToDto(this Photo photo, string? remoteUrl = null, string? thumbnailUrl = null, string? fullUrl = null)
    {
        var uploader = BuildUploaderFields(photo);
        return new PhotoDto
    {
        Id = photo.Id,
        AssetPath = photo.LocalAssetPath ?? remoteUrl ?? string.Empty,
        ThumbnailPath = thumbnailUrl,
        ThumbnailUrl = thumbnailUrl,
        RemoteUrl = remoteUrl,
        FullUrl = fullUrl,
        TakenAt = photo.TakenAt,
        Location = photo.Location,
        Description = photo.Description,
        MemberIds = photo.PhotoMembers.Select(pm => pm.FamilyMemberId).ToList(),
        UserTags = photo.UserTags.OrderBy(t => t.Tag).Select(t => t.Tag).ToList(),
        AiTags = photo.AiTags.OrderBy(t => t.Tag).Select(t => t.Tag).ToList(),
        EventId = photo.EventId,
        UploadedBy = photo.UploadedByUserId,
        UploaderNickname = uploader.Nickname,
        UploaderEmail = uploader.Email,
        Uploader = uploader.Nested,
        Generation = photo.Generation,
        IsHidden = photo.IsHidden,
        IsDuplicate = photo.IsDuplicate,
        IsSimilar = photo.IsSimilar,
        IsBlurry = photo.IsBlurry,
        IsScreenshot = photo.IsScreenshot,
        IsAiInferred = photo.IsAiInferred,
        Visibility = photo.Visibility,
        PrivacyScope = photo.PrivacyScope,
        OriginalFileName = photo.OriginalFileName,
        IsLivePhoto = photo.IsLivePhoto
    };
    }

    private static (string? Nickname, string? Email, PhotoUploaderDto? Nested) BuildUploaderFields(Photo photo)
    {
        var account = photo.UploadedBy;
        if (account is null || account.Id == Guid.Empty)
        {
            return (null, null, null);
        }

        var nickname = string.IsNullOrWhiteSpace(account.Nickname) ? null : account.Nickname.Trim();
        var email = string.IsNullOrWhiteSpace(account.Email) ? null : account.Email.Trim();
        if (nickname is null && email is null)
        {
            return (null, null, null);
        }

        var nested = new PhotoUploaderDto
        {
            Id = account.Id,
            Nickname = nickname,
            Email = email
        };

        return (nickname, email, nested);
    }

    public static MemoryDto ToDto(this Memory memory) => new()
    {
        Id = memory.Id,
        Title = memory.Title,
        Description = memory.Description,
        Type = memory.Type,
        PhotoIds = memory.MemoryPhotos.OrderBy(mp => mp.SortOrder).Select(mp => mp.PhotoId).ToList(),
        VideoIds = memory.MemoryVideos.OrderBy(mv => mv.SortOrder).Select(mv => mv.PhotoId).ToList(),
        TextContent = memory.TextContent,
        MemberIds = memory.MemoryMembers.Select(mm => mm.FamilyMemberId).ToList(),
        StartDate = memory.StartDate,
        EndDate = memory.EndDate,
        Location = memory.Location,
        EventType = memory.EventType,
        Generation = memory.Generation,
        IsAiGenerated = memory.IsAiGenerated,
        IsTodayHighlight = memory.IsTodayHighlight,
        BackgroundMusicId = memory.BackgroundMusicId,
        Visibility = memory.Visibility,
        WeatherSummary = memory.WeatherSummary,
        CreatedAt = memory.CreatedAt
    };

    public static FamilyMemberDto ToDto(this FamilyMember member) => new()
    {
        Id = member.Id,
        Name = member.Name,
        Nickname = member.Nickname,
        BirthDate = member.BirthDate,
        Generation = member.Generation,
        Relationship = member.Relationship,
        AvatarUrl = member.AvatarUrl,
        FaceRecognitionEnabled = member.FaceRecognitionEnabled,
        PhotoIds = member.PhotoMembers.Select(pm => pm.PhotoId).ToList()
    };

    public static FamilyMomentDto ToDto(this FamilyMoment moment) => new()
    {
        Id = moment.Id,
        Name = moment.Name,
        EventType = moment.EventType,
        Date = moment.Date,
        PhotoIds = moment.MomentPhotos.Select(mp => mp.PhotoId).ToList(),
        MemberIds = moment.MomentMembers.Select(mm => mm.FamilyMemberId).ToList(),
        Description = moment.Description,
        IsAiDiscovered = moment.IsAiDiscovered
    };

    public static DisplayDeviceDto ToDto(this DisplayDevice device) => new()
    {
        Id = device.Id,
        Name = device.Name,
        QrCode = device.QrCode,
        Status = device.Status,
        CurrentMemoryId = device.CurrentMemoryId,
        LastSeen = device.LastSeenAt
    };

    public static FriendDto ToDto(this Friend friend) => new()
    {
        Id = friend.Id,
        Name = friend.Name,
        AvatarUrl = friend.AvatarUrl,
        FrameLinked = friend.FrameLinked,
        SharedMemoryCount = friend.SharedMemoryCount
    };

    public static SharedAlbumDto ToDto(this SharedAlbum album) => new()
    {
        Id = album.Id,
        ExternalId = album.ExternalId,
        Name = album.Name,
        Subtitle = album.Subtitle,
        AlbumType = album.AlbumType,
        IsOwn = album.IsOwn
    };

    public static NotificationDto ToDto(this Notification notification) => new()
    {
        Id = notification.Id,
        Message = notification.Message,
        AvatarUrl = notification.AvatarUrl,
        CreatedAt = notification.CreatedAt,
        IsRead = notification.IsRead
    };

    public static FrameCommandDto ToDto(this FrameCommand command) => new()
    {
        Id = command.Id,
        DisplayDeviceId = command.DisplayDeviceId,
        CommandType = command.CommandType,
        Status = command.Status,
        PayloadJson = command.PayloadJson,
        CreatedAt = command.CreatedAt
    };

    public static FramePlaybackPackageDto ToDto(this FramePlaybackPackage package) => new()
    {
        Id = package.Id,
        DisplayDeviceId = package.DisplayDeviceId,
        FamilyId = package.FamilyId,
        ExternalId = package.ExternalId,
        Title = package.Title,
        PackageJson = package.PackageJson,
        IsActive = package.IsActive,
        SortOrder = package.SortOrder
    };

    public static FrameCommentDto ToDto(this FrameComment comment) => new()
    {
        Id = comment.Id,
        PackageId = comment.PackageId,
        UserId = comment.UserId,
        Message = comment.Message,
        CreatedAt = comment.CreatedAt,
        AuthorName = comment.User?.Nickname ?? comment.User?.Email,
        AuthorAvatarUrl = comment.User?.AvatarUrl
    };

    public static PhotoCommentDto ToDto(this PhotoComment comment) => new()
    {
        Id = comment.Id,
        PhotoId = comment.PhotoId,
        Message = comment.Message,
        CreatedAt = comment.CreatedAt,
        AuthorId = comment.UserId,
        UserId = comment.UserId,
        AuthorName = comment.User?.Nickname ?? comment.User?.Email
    };

    public static JournalTagDto ToDto(this JournalTag tag) => new()
    {
        Id = tag.Id,
        LabelKey = tag.LabelKey,
        ColorArgb = tag.ColorArgb,
        IsCustom = tag.IsCustom
    };

    public static PhotoUserTagLibraryEntryDto ToDto(this UserPhotoTagLibraryEntry entry) => new()
    {
        Id = entry.Id,
        Tag = entry.Tag,
        CreatedAt = entry.CreatedAt
    };

    public static PhotoAlbumCommentDto ToDto(this PhotoAlbumComment comment, Guid? viewerUserId = null) => new()
    {
        Id = comment.Id,
        AlbumId = comment.PhotoAlbumId,
        Message = comment.Message,
        CreatedAt = comment.CreatedAt,
        AuthorId = comment.UserId,
        UserId = comment.UserId,
        AuthorName = comment.User?.Nickname ?? comment.User?.Email,
        IsMine = viewerUserId.HasValue && comment.UserId == viewerUserId.Value
    };
}
