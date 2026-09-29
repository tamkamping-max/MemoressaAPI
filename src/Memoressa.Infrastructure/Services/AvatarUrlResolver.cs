using Memoressa.Application.Common;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Domain.Entities;
using Memoressa.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Memoressa.Infrastructure.Services;

public class AvatarUrlResolver : IAvatarUrlResolver
{
    private readonly IS3StorageService _s3;
    private readonly AwsS3Options _options;
    private readonly ILogger<AvatarUrlResolver> _logger;

    public AvatarUrlResolver(
        IS3StorageService s3,
        IOptions<AwsS3Options> options,
        ILogger<AvatarUrlResolver> logger)
    {
        _s3 = s3;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string?> ResolveForResponseAsync(
        string? storedAvatarUrl,
        CancellationToken cancellationToken = default)
    {
        if (!StoredObjectUrl.IsPresignableObjectKey(storedAvatarUrl))
        {
            return storedAvatarUrl;
        }

        try
        {
            var expiry = TimeSpan.FromMinutes(_options.PresignedUrlExpiryMinutes);
            return await _s3.GetPresignedGetUrlAsync(storedAvatarUrl!, expiry, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Could not presign avatar URL for key {AvatarKey}; returning stored key",
                storedAvatarUrl);
            return storedAvatarUrl;
        }
    }

    public async Task<UserDto> ToUserDtoAsync(UserAccount user, CancellationToken cancellationToken = default)
    {
        var dto = user.ToDto();
        return dto with { AvatarUrl = await ResolveForResponseAsync(dto.AvatarUrl, cancellationToken) };
    }

    public async Task<FamilyMemberDto> ToFamilyMemberDtoAsync(
        FamilyMember member,
        CancellationToken cancellationToken = default)
    {
        var dto = member.ToDto();
        return dto with { AvatarUrl = await ResolveForResponseAsync(dto.AvatarUrl, cancellationToken) };
    }
}
