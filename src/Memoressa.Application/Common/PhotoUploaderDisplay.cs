using Memoressa.Application.DTOs;
using Memoressa.Domain.Entities;

namespace Memoressa.Application.Common;

public readonly record struct PhotoUploaderResolvedFields(
    Guid UploadedBy,
    string? Nickname,
    string? Email,
    PhotoUploaderDto Uploader,
    string? DisplayName);

public static class PhotoUploaderDisplay
{
    public static PhotoUploaderResolvedFields Resolve(Photo photo, string? familyMemberDisplayName = null)
    {
        var uploadedBy = photo.UploadedByUserId;
        var account = photo.UploadedBy;
        if (account is not null && account.Id != Guid.Empty)
        {
            uploadedBy = account.Id;
        }

        var accountNickname = TrimOrNull(account?.Nickname);
        var email = TrimOrNull(account?.Email);
        var familyLabel = TrimOrNull(familyMemberDisplayName);

        var nickname = accountNickname ?? familyLabel ?? email;
        var displayName = nickname;

        var uploader = new PhotoUploaderDto
        {
            Id = uploadedBy,
            Nickname = nickname,
            Email = email
        };

        return new PhotoUploaderResolvedFields(uploadedBy, nickname, email, uploader, displayName);
    }

    public static PhotoDto ApplyToDto(PhotoDto dto, PhotoUploaderResolvedFields fields) =>
        dto with
        {
            UploadedBy = fields.UploadedBy,
            UploaderNickname = fields.Nickname,
            UploaderEmail = fields.Email,
            Uploader = fields.Uploader,
            UploaderDisplayName = fields.DisplayName
        };

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
