using Memoressa.Application.Abstractions;
using Memoressa.Application.Interfaces;
using Memoressa.Infrastructure.Data;
using Memoressa.Infrastructure.Options;
using Memoressa.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Memoressa.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<AwsS3Options>(configuration.GetSection(AwsS3Options.SectionName));
        services.Configure<AiOptions>(configuration.GetSection(AiOptions.SectionName));
        services.Configure<InternalApiOptions>(configuration.GetSection(InternalApiOptions.SectionName));

        services.AddDbContext<MemoressaDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IMemoressaDbContext>(sp => sp.GetRequiredService<MemoressaDbContext>());

        services.AddHttpContextAccessor();
        services.AddHttpClient("OpenAi", (sp, client) =>
        {
            var aiOptions = configuration.GetSection(AiOptions.SectionName).Get<AiOptions>() ?? new AiOptions();
            client.BaseAddress = new Uri(aiOptions.OpenAiBaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromMinutes(2);
        });

        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IS3StorageService, S3StorageService>();
        services.AddScoped<IPhotoUrlResolver, PhotoUrlResolver>();
        services.AddScoped<IThumbnailGenerationService, ThumbnailGenerationService>();
        services.AddScoped<IAiOrchestrationService, AiOrchestrationService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        return services;
    }
}
