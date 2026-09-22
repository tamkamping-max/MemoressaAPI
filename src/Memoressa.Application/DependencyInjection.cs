using Memoressa.Application.Interfaces;
using Memoressa.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Memoressa.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IMemoryService, MemoryService>();
        services.AddScoped<IPhotoService, PhotoService>();
        services.AddScoped<IFamilyService, FamilyService>();
        services.AddScoped<IFamilyMomentService, FamilyMomentService>();
        services.AddScoped<IDisplayDeviceService, DisplayDeviceService>();
        services.AddScoped<IAiService, AiService>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<IFriendService, FriendService>();
        services.AddScoped<ISharedAlbumService, SharedAlbumService>();
        services.AddScoped<IUploadService, UploadService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IFrameService, FrameService>();
        services.AddScoped<IInternalRealtimeService, InternalRealtimeService>();

        return services;
    }
}
