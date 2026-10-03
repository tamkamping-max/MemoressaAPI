namespace Memoressa.Application.Common;

public static class ActivityErrorCodes
{
    public const string ActivityAlbumAmbiguous = "activity_album_ambiguous";

    public const string ActivityAlbumAmbiguousMessage =
        "Multiple activities match; pass creatorUserId or use the activity album id (GUID)";
}
