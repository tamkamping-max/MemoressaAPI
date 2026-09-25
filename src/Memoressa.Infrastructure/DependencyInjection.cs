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
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<AwsS3Options>(configuration.GetSection(AwsS3Options.SectionName));
        services.Configure<AiOptions>(configuration.GetSection(AiOptions.SectionName));
        services.Configure<InternalApiOptions>(configuration.GetSection(InternalApiOptions.SectionName));

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
        services.AddScoped<IOpenAiAgentService, OpenAiAgentService>();
        services.AddScoped<IAiOrchestrationService, AiOrchestrationService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        return services;
    }
}
