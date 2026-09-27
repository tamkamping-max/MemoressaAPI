using Memoressa.Application.Common;
using Memoressa.Application.Interfaces;
using Memoressa.Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Memoressa.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OAuthSettings>(configuration.GetSection(OAuthSettings.SectionName));
        services.Configure<MediaStorageSettings>(configuration.GetSection(MediaStorageSettings.SectionName));
        services.Configure<StorageQuotaSettings>(configuration.GetSection(StorageQuotaSettings.SectionName));
        services.AddHttpClient("GoogleOAuth");
        services.AddHttpClient("FacebookOAuth");

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IMemoryService, MemoryService>();
        services.AddScoped<IPhotoService, PhotoService>();
        services.AddScoped<IFamilyService, FamilyService>();
        services.AddScoped<IFamilyMomentService, FamilyMomentService>();
        services.AddScoped<IDisplayDeviceService, DisplayDeviceService>();
        services.AddScoped<IAiService, AiService>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<IFriendService, FriendService>();
        services.AddScoped<IJournalTagService, JournalTagService>();
        services.AddScoped<IPhotoUserTagLibraryService, PhotoUserTagLibraryService>();
        services.AddScoped<IActivityService, ActivityService>();
        services.AddScoped<ISharedAlbumService, SharedAlbumService>();
        services.AddScoped<IUploadService, UploadService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IFrameService, FrameService>();
        services.AddScoped<IInternalRealtimeService, InternalRealtimeService>();
        services.AddScoped<IAiAgentService, AiAgentService>();

        return services;
    }
}
