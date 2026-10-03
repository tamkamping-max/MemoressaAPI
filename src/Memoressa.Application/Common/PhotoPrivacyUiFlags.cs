using Memoressa.Domain.Enums;

namespace Memoressa.Application.Common;

public readonly record struct PhotoPrivacyUiFlags(
    bool PrivacyOnlySelf,
    bool PrivacyFamily,
    bool PrivacyFriends,
    bool PrivacyCustomList)
{
    public static PhotoPrivacyUiFlags FromScope(UploadPrivacyScope scope) =>
        scope switch
        {
            UploadPrivacyScope.OnlySelf => new PhotoPrivacyUiFlags(true, false, false, false),
            UploadPrivacyScope.Family => new PhotoPrivacyUiFlags(false, true, false, false),
            UploadPrivacyScope.Friends => new PhotoPrivacyUiFlags(false, false, true, false),
            UploadPrivacyScope.FriendsAndFamily => new PhotoPrivacyUiFlags(false, true, true, false),
            UploadPrivacyScope.Custom => new PhotoPrivacyUiFlags(false, false, false, true),
            _ => new PhotoPrivacyUiFlags(false, true, false, false)
        };
}
