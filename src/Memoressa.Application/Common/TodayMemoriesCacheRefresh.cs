using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;

namespace Memoressa.Application.Common;

public static class TodayMemoriesCacheRefresh
{
    /// <summary>
    /// When the first call of the day cached an empty 今日回憶, allow a later call to recompose if the family now has enough eligible photos.
    /// </summary>
    public static bool ShouldRecomposeEmptyCache(TodayMemoriesCache cache, int eligiblePhotoCount)
    {
        if (cache.Strategy != TodayMemoriesStrategy.Empty)
        {
            return false;
        }

        if (TodayMemoriesCacheCodec.Deserialize(cache.ItemsJson).Count > 0)
        {
            return false;
        }

        return eligiblePhotoCount >= TodayMemoriesConstants.MinPhotos;
    }

    public static bool IsEligiblePhoto(Photo photo) =>
        !photo.IsHidden
        && (!string.IsNullOrWhiteSpace(photo.S3Key) || !string.IsNullOrWhiteSpace(photo.LocalAssetPath));
}
