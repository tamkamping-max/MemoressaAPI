namespace Memoressa.Domain.Enums;

public enum Generation
{
    Eldest,
    Parent,
    Self,
    Child,
    Grandchild,
    GreatGrandchild,
    GreatGreatGrandchild
}

public enum MemoryType
{
    Photo,
    Video,
    Text,
    FamilyDiary,
    AiMemory,
    FamilyMoment
}

public enum MemoryVisibility
{
    Private,
    Family,
    SpecificMembers
}

public enum EventType
{
    Birthday,
    Wedding,
    Anniversary,
    Holiday,
    FamilyGathering,
    Newborn,
    Graduation,
    Moving,
    Travel,
    PetGrowth,
    Dinner,
    Other
}

public enum AiInferenceStatus
{
    Pending,
    Confirmed,
    Rejected
}

public enum DisplayDeviceStatus
{
    Online,
    Offline
}

public enum TransitionType
{
    Fade,
    SlideLeft,
    SlideRight,
    SlideUp,
    SlideDown,
    ZoomIn,
    ZoomOut,
    CrossFade,
    KenBurns,
    Blur,
    Rotate,
    Flip,
    Dissolve,
    Wipe,
    Cinematic
}

public enum UploadPrivacyScope
{
    OnlySelf,
    Family,
    Friends,
    FriendsAndFamily,
    Custom
}

public enum FrameCommandType
{
    PlayMemory,
    PlayPackage,
    Pause,
    Resume,
    Stop,
    SyncQueue,
    PushComment,
    UpdateStatus,
    ClearFamilySharedContent = 9
}

public enum FrameCommandStatus
{
    Pending,
    Delivered,
    Acknowledged,
    Failed
}

public enum AiAnalysisJobStatus
{
    Queued,
    Running,
    Completed,
    Failed
}

public enum OAuthProvider
{
    Google,
    Facebook,
    Apple
}

public enum SharedAlbumType
{
    Personal,
    Family,
    FriendTrip,
    Custom
}

public enum MediaKind
{
    Photo,
    Video,
    Thumbnail
}

public enum UploadSessionStatus
{
    Pending,
    Uploading,
    Completed,
    Failed,
    Expired
}

public enum TodayMemoriesStrategy
{
    Empty = 0,
    YearsAgoToday = 1,
    RandomFallback = 2
}

public enum ActivityAlbumType
{
    Travel = 0,
    Wedding = 1,
    Conference = 2,
    Concert = 3,
    Gathering = 4,
    Other = 5
}

public enum ActivityAlbumStatus
{
    InProgress = 0,
    Completed = 1,
    Cancelled = 2
}

public enum FriendInviteStatus
{
    Pending = 0,
    Accepted = 1,
    Rejected = 2
}
