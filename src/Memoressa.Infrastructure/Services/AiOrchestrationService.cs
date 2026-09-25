using System.Net.Http.Json;
using System.Text.Json;
using Memoressa.Application.Abstractions;
using Memoressa.Application.Common;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;
using Memoressa.Infrastructure.Data;
using Memoressa.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Memoressa.Infrastructure.Services;

public class AiOrchestrationService : IAiOrchestrationService
{
    private readonly IMemoressaDbContext _db;
    private readonly AiOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AiOrchestrationService> _logger;
    private readonly IPhotoUrlResolver _photoUrls;
    private readonly Random _random = new();

    private static readonly TransitionType[] Transitions =
    [
        TransitionType.Fade,
        TransitionType.KenBurns,
        TransitionType.CrossFade,
        TransitionType.ZoomIn
    ];

    public AiOrchestrationService(
        IMemoressaDbContext db,
        IOptions<AiOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<AiOrchestrationService> logger,
        IPhotoUrlResolver photoUrls)
    {
        _db = db;
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _photoUrls = photoUrls;
    }

    public async Task<AiAnalysisResultDto> AnalyzePhotosAsync(
        Guid userId,
        Guid familyId,
        IReadOnlyList<Guid> photoIds,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Photos.AsNoTracking().Where(p => p.FamilyId == familyId && !p.IsHidden);
        if (photoIds.Count > 0)
        {
            query = query.Where(p => photoIds.Contains(p.Id));
        }

        var photos = await query
            .Include(p => p.AiTags)
            .ToListAsync(cancellationToken);

        var members = await _db.FamilyMembers.AsNoTracking()
            .Where(m => m.FamilyId == familyId)
            .ToListAsync(cancellationToken);

        var job = new AiAnalysisJob
        {
            UserId = userId,
            FamilyId = familyId,
            Status = AiAnalysisJobStatus.Running,
            PhotoIdsJson = JsonSerializer.Serialize(photos.Select(p => p.Id))
        };
        _db.AiAnalysisJobs.Add(job);
        await _db.SaveChangesAsync(cancellationToken);

        if (_options.EnableVisionBatch && !string.IsNullOrWhiteSpace(_options.OpenAiApiKey))
        {
            _ = Task.Run(() => ProcessVisionBatchAsync(job.Id, CancellationToken.None), cancellationToken);
        }
        else
        {
            await ApplyHeuristicAnalysisAsync(photos, members, cancellationToken);
        }

        var dates = photos.Where(p => p.TakenAt.HasValue).Select(p => p.TakenAt!.Value).ToList();
        var result = new AiAnalysisResultDto
        {
            PhotoCount = photos.Count,
            PotentialMembers = Math.Max(members.Count, Math.Min(7, photos.Count / 3)),
            PotentialMemories = Math.Max(1, photos.Count / 5),
            PotentialEvents = Math.Max(1, photos.Count / 10),
            EarliestDate = dates.Count > 0 ? dates.Min() : null,
            LatestDate = dates.Count > 0 ? dates.Max() : null,
            SuggestedMemberNames = members.Select(m => m.Name).Take(5).ToList()
        };

        job.Status = AiAnalysisJobStatus.Completed;
        job.CompletedAt = DateTime.UtcNow;
        job.ResultJson = JsonSerializer.Serialize(result);
        await _db.SaveChangesAsync(cancellationToken);

        return result;
    }

    public async Task<IReadOnlyList<SearchResultDto>> SearchMemoriesAsync(
        Guid familyId,
        string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var pattern = EfTextSearch.ToLikePattern(query);
        var usePostgreSql = EfTextSearch.IsPostgreSqlProvider(_db);

        var memories = await EfTextSearch
            .WhereMemoryTextMatches(_db.Memories.AsNoTracking().Where(m => m.FamilyId == familyId), pattern, usePostgreSql)
            .Include(m => m.MemoryPhotos)
            .ToListAsync(cancellationToken);

        var tagMatches = await EfTextSearch
            .WherePhotoTagMatches(_db.Photos.AsNoTracking().Where(p => p.FamilyId == familyId), pattern, usePostgreSql)
            .Include(p => p.MemoryPhotos)
            .Include(p => p.AiTags)
            .ToListAsync(cancellationToken);

        var memoryIdsFromTags = tagMatches
            .SelectMany(p => p.MemoryPhotos.Select(mp => mp.MemoryId))
            .Distinct()
            .ToHashSet();

        var tagMemories = memoryIdsFromTags.Count == 0
            ? []
            : await _db.Memories.AsNoTracking()
                .Where(m => memoryIdsFromTags.Contains(m.Id))
                .Include(m => m.MemoryPhotos)
                .ToListAsync(cancellationToken);

        var combined = memories
            .Concat(tagMemories)
            .GroupBy(m => m.Id)
            .Select(g => g.First())
            .ToList();

        var photoLookup = await _db.Photos.AsNoTracking()
            .Where(p => p.FamilyId == familyId)
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var results = new List<SearchResultDto>();
        foreach (var m in combined)
        {
            var reasons = new List<string>();
            if (m.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("Title match");
            }

            if (m.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
            {
                reasons.Add("Description match");
            }

            if (tagMemories.Any(tm => tm.Id == m.Id))
            {
                reasons.Add("AI tag match");
            }

            if (reasons.Count == 0)
            {
                reasons.Add("Semantic match");
            }

            string? thumb = null;
            var firstPhotoId = m.MemoryPhotos.OrderBy(mp => mp.SortOrder).Select(mp => mp.PhotoId).FirstOrDefault();
            if (firstPhotoId != Guid.Empty && photoLookup.TryGetValue(firstPhotoId, out var photo))
            {
                thumb = await _photoUrls.GetPresignedUrlAsync(photo, thumbnail: true, cancellationToken: cancellationToken)
                    ?? photo.LocalAssetPath;
            }

            results.Add(new SearchResultDto
            {
                MemoryId = m.Id,
                Title = m.Title,
                ThumbnailPath = thumb,
                MatchReasons = reasons,
                RelevanceScore = 0.75 + _random.NextDouble() * 0.25
            });
        }

        return results.OrderByDescending(r => r.RelevanceScore).ToList();
    }

    public async Task<IReadOnlyList<PlaybackItemDto>> GeneratePlaybackAsync(
        Guid familyId,
        PlaybackRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var photosQuery = _db.Photos.AsNoTracking()
            .Where(p => p.FamilyId == familyId && !p.IsHidden)
            .Include(p => p.PhotoMembers)
            .ThenInclude(pm => pm.FamilyMember)
            .AsQueryable();

        if (request.PhotoIds is { Count: > 0 })
        {
            photosQuery = photosQuery.Where(p => request.PhotoIds.Contains(p.Id));
        }

        if (request.MemberId.HasValue)
        {
            photosQuery = photosQuery.Where(p => p.PhotoMembers.Any(pm => pm.FamilyMemberId == request.MemberId.Value));
        }

        if (request.Generation.HasValue)
        {
            photosQuery = photosQuery.Where(p => p.Generation == request.Generation.Value);
        }

        if (request.Year.HasValue)
        {
            photosQuery = photosQuery.Where(p => p.TakenAt.HasValue && p.TakenAt.Value.Year == request.Year.Value);
        }

        var photos = await photosQuery.OrderByDescending(p => p.TakenAt).Take(100).ToListAsync(cancellationToken);
        if (request.AiCurated)
        {
            photos = photos
                .OrderByDescending(p => p.AiTags.Count)
                .ThenByDescending(p => p.TakenAt)
                .Take(30)
                .ToList();
        }

        var items = new List<PlaybackItemDto>();
        for (var index = 0; index < photos.Count; index++)
        {
            var photo = photos[index];
            var assetPath = await _photoUrls.GetPresignedUrlAsync(photo, thumbnail: false, cancellationToken: cancellationToken)
                ?? photo.LocalAssetPath
                ?? string.Empty;

            items.Add(new PlaybackItemDto
            {
                PhotoId = photo.Id,
                AssetPath = assetPath,
                Title = photo.Description,
                Description = photo.Location,
                Date = photo.TakenAt,
                MemberNames = photo.PhotoMembers.Select(pm => pm.FamilyMember.Name).Distinct().ToList(),
                Generation = photo.Generation,
                Transition = Transitions[index % Transitions.Length],
                DisplayDurationSeconds = 5
            });
        }

        return items;
    }

    public async Task<MemoryDto> CreateAiMemoryAsync(
        Guid userId,
        Guid familyId,
        IReadOnlyList<Guid> photoIds,
        CancellationToken cancellationToken = default)
    {
        var photos = await _db.Photos
            .Where(p => p.FamilyId == familyId && photoIds.Contains(p.Id))
            .Include(p => p.PhotoMembers)
            .ToListAsync(cancellationToken);

        if (photos.Count == 0)
        {
            throw new ApiException("No photos found for AI memory", 404);
        }

        var memberIds = photos.SelectMany(p => p.PhotoMembers.Select(pm => pm.FamilyMemberId)).Distinct().ToList();
        var locations = photos.Select(p => p.Location).Where(l => !string.IsNullOrWhiteSpace(l)).Distinct().ToList();
        var dates = photos.Where(p => p.TakenAt.HasValue).Select(p => p.TakenAt!.Value).ToList();

        var memory = new Memory
        {
            FamilyId = familyId,
            CreatedByUserId = userId,
            Title = "AI Memory",
            Description = "Automatically curated memory",
            Type = MemoryType.AiMemory,
            IsAiGenerated = true,
            StartDate = dates.Count > 0 ? dates.Min() : DateTime.UtcNow,
            EndDate = dates.Count > 0 ? dates.Max() : null,
            Location = locations.FirstOrDefault(),
            Visibility = MemoryVisibility.Family
        };

        _db.Memories.Add(memory);

        var order = 0;
        foreach (var photoId in photoIds)
        {
            _db.MemoryPhotos.Add(new MemoryPhoto
            {
                MemoryId = memory.Id,
                PhotoId = photoId,
                SortOrder = order++
            });
        }

        foreach (var memberId in memberIds)
        {
            _db.MemoryMembers.Add(new MemoryMember
            {
                MemoryId = memory.Id,
                FamilyMemberId = memberId
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        memory.MemoryPhotos = photoIds.Select((id, idx) => new MemoryPhoto
        {
            MemoryId = memory.Id,
            PhotoId = id,
            SortOrder = idx
        }).ToList();
        memory.MemoryMembers = memberIds.Select(id => new MemoryMember
        {
            MemoryId = memory.Id,
            FamilyMemberId = id
        }).ToList();

        return memory.ToDto();
    }

    public async Task ProcessVisionBatchAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _db.AiAnalysisJobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (job is null)
        {
            return;
        }

        try
        {
            var photoIds = JsonSerializer.Deserialize<List<Guid>>(job.PhotoIdsJson) ?? [];
            var photos = await _db.Photos
                .Where(p => photoIds.Contains(p.Id))
                .Take(_options.MaxBatchSize)
                .ToListAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(_options.OpenAiApiKey))
            {
                await ApplyHeuristicAnalysisAsync(photos, [], cancellationToken);
                return;
            }

            var client = _httpClientFactory.CreateClient("OpenAi");
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.OpenAiApiKey);

            foreach (var photo in photos)
            {
                var imageUrl = await _photoUrls.GetPresignedUrlAsync(
                    photo,
                    thumbnail: false,
                    PhotoUrlPurpose.AiProcessing,
                    cancellationToken);

                if (string.IsNullOrWhiteSpace(imageUrl))
                {
                    continue;
                }

                var payload = new
                {
                    model = _options.VisionModel,
                    messages = new object[]
                    {
                        new
                        {
                            role = "user",
                            content = new object[]
                            {
                                new { type = "text", text = "Describe this family photo briefly and list up to 5 tags." },
                                new { type = "image_url", image_url = new { url = imageUrl } }
                            }
                        }
                    },
                    max_tokens = 300
                };

                var response = await client.PostAsJsonAsync("chat/completions", payload, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("OpenAI vision call failed for photo {PhotoId}: {Status}", photo.Id, response.StatusCode);
                    continue;
                }

                var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
                var content = json.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
                photo.AiAnalysisJson = content;
                photo.Description ??= content.Split('\n').FirstOrDefault()?.Trim();

                foreach (var tag in ExtractTags(content))
                {
                    _db.PhotoAiTags.Add(new PhotoAiTag { PhotoId = photo.Id, Tag = tag });
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            job.Status = AiAnalysisJobStatus.Failed;
            job.ErrorMessage = ex.Message;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task ApplyHeuristicAnalysisAsync(
        IReadOnlyList<Photo> photos,
        IReadOnlyList<FamilyMember> members,
        CancellationToken cancellationToken)
    {
        foreach (var photo in photos)
        {
            if (photo.AiTags.Count > 0)
            {
                continue;
            }

            var tags = new List<string>();
            if (!string.IsNullOrWhiteSpace(photo.Location))
            {
                tags.Add(photo.Location);
            }

            if (photo.TakenAt.HasValue)
            {
                tags.Add(photo.TakenAt.Value.ToString("MMMM"));
                tags.Add(photo.TakenAt.Value.Year.ToString());
            }

            tags.Add("family");

            foreach (var tag in tags.Distinct(StringComparer.OrdinalIgnoreCase).Take(5))
            {
                _db.PhotoAiTags.Add(new PhotoAiTag { PhotoId = photo.Id, Tag = tag });
            }

            if (members.Count > 0 && _random.NextDouble() > 0.5)
            {
                var member = members[_random.Next(members.Count)];
                _db.PhotoAiInferences.Add(new PhotoAiInference
                {
                    PhotoId = photo.Id,
                    SuggestedMemberId = member.Id,
                    SuggestedMemberName = member.Name,
                    Confidence = 0.6 + _random.NextDouble() * 0.3,
                    Status = AiInferenceStatus.Pending
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static IEnumerable<string> ExtractTags(string content)
    {
        var tags = content
            .Split([' ', ',', '\n', '.', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(w => w.Length is >= 3 and <= 32)
            .Take(5);
        return tags;
    }
}
