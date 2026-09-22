using Memoressa.Application.DTOs;
using Memoressa.Domain.Entities;

namespace Memoressa.Application.Interfaces;

public interface ITokenService
{
    AuthTokensDto GenerateTokens(UserAccount user);
    Guid? ValidateAccessToken(string token);
    Task<RefreshToken?> ValidateRefreshTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<RefreshToken> CreateRefreshTokenAsync(Guid userId, CancellationToken cancellationToken = default);
    Task RevokeRefreshTokenAsync(string token, CancellationToken cancellationToken = default);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface IS3StorageService
{
    string BuildObjectKey(Guid familyId, Guid userId, string fileName);
    Task<string> GetPresignedPutUrlAsync(string s3Key, string contentType, TimeSpan expiry, CancellationToken cancellationToken = default);
    Task<string> GetPresignedGetUrlAsync(string s3Key, TimeSpan expiry, CancellationToken cancellationToken = default);
    Task<byte[]> GetObjectBytesAsync(string s3Key, CancellationToken cancellationToken = default);
    Task PutObjectAsync(string s3Key, byte[] bytes, string contentType, CancellationToken cancellationToken = default);
    string BuildThumbnailKey(string originalS3Key);
}

public interface IAiOrchestrationService
{
    Task<AiAnalysisResultDto> AnalyzePhotosAsync(Guid userId, Guid familyId, IReadOnlyList<Guid> photoIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SearchResultDto>> SearchMemoriesAsync(Guid familyId, string query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PlaybackItemDto>> GeneratePlaybackAsync(Guid familyId, PlaybackRequestDto request, CancellationToken cancellationToken = default);
    Task<MemoryDto> CreateAiMemoryAsync(Guid userId, Guid familyId, IReadOnlyList<Guid> photoIds, CancellationToken cancellationToken = default);
    Task ProcessVisionBatchAsync(Guid jobId, CancellationToken cancellationToken = default);
}

public interface IEmailService
{
    Task SendPasswordResetAsync(string email, string resetToken, CancellationToken cancellationToken = default);
}

public interface ICurrentUserService
{
    Guid? UserId { get; }
    Guid? FamilyId { get; }
    bool IsAuthenticated { get; }
}
