using Memoressa.Domain.Entities;

namespace Memoressa.Application.Common;

public enum ActivityAlbumAmbiguityPolicy
{
    /// <summary>When multiple accessible albums match, prefer <c>CreatorUserId == viewer</c>, then newest.</summary>
    PreferViewerAsCreator,

    /// <summary>When still ambiguous after filters, treat as failure (caller returns 409).</summary>
    FailIfAmbiguous
}

public readonly record struct ActivityAlbumResolveRequest(
    string ActivityId,
    Guid? CreatorUserId = null,
    Guid? HomeFamilyId = null,
    bool ForUpdate = false,
    ActivityAlbumAmbiguityPolicy AmbiguityPolicy = ActivityAlbumAmbiguityPolicy.PreferViewerAsCreator);

public readonly record struct ActivityAlbumResolveResult(
    ActivityAlbum? Activity,
    string? Error,
    int StatusCode)
{
    public bool Success => Activity is not null && StatusCode is >= 200 and < 300;

    public static ActivityAlbumResolveResult Found(ActivityAlbum activity) =>
        new(activity, null, 200);

    public static ActivityAlbumResolveResult NotFound() =>
        new(null, "Activity not found", 404);

    public static ActivityAlbumResolveResult Ambiguous() =>
        new(
            null,
            "Multiple activities match; pass creatorUserId or use the activity album id (GUID)",
            409);

    public static ActivityAlbumResolveResult Forbidden() =>
        new(null, "Forbidden", 403);
}
