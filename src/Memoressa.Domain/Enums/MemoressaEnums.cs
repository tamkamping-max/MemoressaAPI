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
    UpdateStatus
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
    Facebook
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
