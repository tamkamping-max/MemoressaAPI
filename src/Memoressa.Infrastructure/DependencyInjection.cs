using Memoressa.Application.Abstractions;
using Memoressa.Application.Interfaces;
using Memoressa.Infrastructure.Data;
using Memoressa.Infrastructure.Options;
using Memoressa.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Memoressa.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<AwsS3Options>(configuration.GetSection(AwsS3Options.SectionName));
        services.Configure<AwsSesOptions>(configuration.GetSection(AwsSesOptions.SectionName));
        services.Configure<AiOptions>(configuration.GetSection(AiOptions.SectionName));
        services.PostConfigure<AiOptions>(options =>
        {
            if (string.IsNullOrWhiteSpace(options.GrokApiKey))
            {
                var xaiKey = Environment.GetEnvironmentVariable("XAI_API_KEY");
                if (!string.IsNullOrWhiteSpace(xaiKey))
                {
                    options.GrokApiKey = xaiKey;
                }
            }

            var legacyKey = configuration["Ai:OpenAiApiKey"];
            if (string.IsNullOrWhiteSpace(options.GrokApiKey) && !string.IsNullOrWhiteSpace(legacyKey))
            {
                options.GrokApiKey = legacyKey;
            }

            if (string.IsNullOrWhiteSpace(options.GrokBaseUrl))
            {
                var legacyBase = configuration["Ai:OpenAiBaseUrl"];
                if (!string.IsNullOrWhiteSpace(legacyBase))
                {
                    options.GrokBaseUrl = legacyBase;
                }
            }
        });
        services.Configure<InternalApiOptions>(configuration.GetSection(InternalApiOptions.SectionName));

        services.AddHostedService<UploadSessionCleanupHostedService>();

        var connectionString = DatabaseConnection.ResolveConnectionString(configuration);

        services.AddDbContext<MemoressaDbContext>(options =>
        {
            if (DatabaseConnection.UsesPostgreSql(configuration))
            {
                options.UseNpgsql(connectionString);
                return;
            }

            options.UseMySql(
                connectionString,
                ServerVersion.Parse("8.0.0-mysql"),
                mySqlOptions => mySqlOptions.EnableStringComparisonTranslations());
        });

        services.AddScoped<IMemoressaDbContext>(sp => sp.GetRequiredService<MemoressaDbContext>());

        services.AddHttpContextAccessor();
        services.AddHttpClient("Grok", (sp, client) =>
        {
            var aiOptions = sp.GetRequiredService<IOptions<AiOptions>>().Value;
            client.BaseAddress = new Uri(aiOptions.GrokBaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromMinutes(2);
        });

        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IS3StorageService, S3StorageService>();
        services.AddScoped<IPhotoUrlResolver, PhotoUrlResolver>();
        services.AddScoped<IAvatarUrlResolver, AvatarUrlResolver>();
        services.AddScoped<IThumbnailGenerationService, ThumbnailGenerationService>();
        services.AddScoped<IGrokAgentService, GrokAgentService>();
        services.AddScoped<IAiOrchestrationService, AiOrchestrationService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddMemoryCache();
        services.AddSingleton<IDisplayDevicePairingSessionStore, DisplayDevicePairingSessionStore>();
        services.AddHttpClient("AppleOAuth");
        services.AddScoped<IAppleSignInValidator, AppleSignInValidator>();

        return services;
    }
}
