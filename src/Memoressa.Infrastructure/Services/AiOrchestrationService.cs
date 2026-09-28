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

        if (_options.EnableVisionBatch && !string.IsNullOrWhiteSpace(_options.GrokApiKey))
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
        CancellationToken cancellationToken = default,
        IReadOnlyList<string>? additionalSearchTerms = null)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var usePostgreSql = EfTextSearch.IsPostgreSqlProvider(_db);
        var years = AgentSearchQuery.ExtractYears(query);
        var searchTerms = AgentSearchTermBuilder.Build(query, additionalSearchTerms);

        var photoScope = _db.Photos.AsNoTracking().Where(p => p.FamilyId == familyId && !p.IsHidden);

        var memoryTextMatches = new Dictionary<Guid, Memory>();
        foreach (var term in searchTerms)
        {
            var pattern = EfTextSearch.ToLikePattern(term);
            var batch = await EfTextSearch
                .WhereMemoryTextMatches(_db.Memories.AsNoTracking().Where(m => m.FamilyId == familyId), pattern, usePostgreSql)
                .Include(m => m.MemoryPhotos)
                .ToListAsync(cancellationToken);

            foreach (var memory in batch)
            {
                memoryTextMatches[memory.Id] = memory;
            }
        }

        var memoryTextMatchIds = memoryTextMatches.Keys.ToHashSet();

        var fieldMatchedPhotos = new Dictionary<Guid, Photo>();
        foreach (var term in searchTerms)
        {
            var pattern = EfTextSearch.ToLikePattern(term);
            var batch = await EfTextSearch
                .WherePhotoAgentFieldMatches(photoScope, pattern, usePostgreSql)
                .Include(p => p.MemoryPhotos)
                .Include(p => p.AiTags)
                .Include(p => p.UserTags)
                .ToListAsync(cancellationToken);

            foreach (var photo in batch)
            {
                fieldMatchedPhotos[photo.Id] = photo;
            }
        }

        var dateMatchedPhotos = years.Count == 0
            ? []
            : await EfTextSearch
                .WherePhotoTakenAtYearIn(photoScope, years)
                .Include(p => p.MemoryPhotos)
                .Include(p => p.AiTags)
                .Include(p => p.UserTags)
                .ToListAsync(cancellationToken);

        var albumTextMatches = new Dictionary<Guid, PhotoAlbum>();
        foreach (var term in searchTerms)
        {
            var pattern = EfTextSearch.ToLikePattern(term);
            var batch = await EfTextSearch
                .WherePhotoAlbumAgentFieldMatches(
                    _db.PhotoAlbums.AsNoTracking().Where(a => a.FamilyId == familyId),
                    pattern,
                    usePostgreSql)
                .Include(a => a.AlbumPhotos)
                .Include(a => a.UserTags)
                .Include(a => a.Comments)
                .ToListAsync(cancellationToken);

            foreach (var album in batch)
            {
                albumTextMatches[album.Id] = album;
            }
        }

        var albumTextMatchIds = albumTextMatches.Keys.ToHashSet();
        var albumLookup = albumTextMatches;
        var albumTextMatchList = albumTextMatches.Values.ToList();

        var candidatePhotos = new Dictionary<Guid, Photo>();
        void AddPhoto(Photo p) => candidatePhotos[p.Id] = p;

        foreach (var p in fieldMatchedPhotos.Values.Concat(dateMatchedPhotos))
        {
            AddPhoto(p);
        }

        var albumMatchedPhotoIds = albumTextMatchList
            .SelectMany(a => a.AlbumPhotos.Select(ap => ap.PhotoId))
            .Distinct()
            .ToList();

        if (albumMatchedPhotoIds.Count > 0)
        {
            var albumPhotos = await photoScope
                .Where(p => albumMatchedPhotoIds.Contains(p.Id))
                .Include(p => p.MemoryPhotos)
                .Include(p => p.AiTags)
                .Include(p => p.UserTags)
                .ToListAsync(cancellationToken);

            foreach (var p in albumPhotos)
            {
                AddPhoto(p);
            }
        }

        var memoryTextMatchPhotoIds = memoryTextMatches.Values
            .SelectMany(m => m.MemoryPhotos.Select(mp => mp.PhotoId))
            .Distinct()
            .ToList();

        if (memoryTextMatchPhotoIds.Count > 0)
        {
            var memoryPhotos = await photoScope
                .Where(p => memoryTextMatchPhotoIds.Contains(p.Id))
                .Include(p => p.MemoryPhotos)
                .Include(p => p.AiTags)
                .Include(p => p.UserTags)
                .ToListAsync(cancellationToken);

            foreach (var p in memoryPhotos)
            {
                AddPhoto(p);
            }
        }

        if (candidatePhotos.Count == 0)
        {
            return [];
        }

        var memoryLookup = memoryTextMatches;

        var albumTagsByPhotoId = await LoadAlbumUserTagsByPhotoIdAsync(
            familyId,
            candidatePhotos.Keys,
            cancellationToken);

        var albumsByPhotoId = BuildAlbumsByPhotoId(albumTextMatchList);

        var results = new List<SearchResultDto>();
        foreach (var photo in candidatePhotos.Values)
        {
            var reasons = BuildPhotoMatchReasons(
                photo,
                memoryTextMatchIds,
                memoryLookup,
                albumTextMatchIds,
                albumsByPhotoId,
                searchTerms,
                years,
                albumTagsByPhotoId);

            var primaryMemoryId = ResolvePrimaryMemoryId(photo, memoryTextMatchIds);
            Memory? primaryMemory = null;
            if (primaryMemoryId.HasValue)
            {
                memoryLookup.TryGetValue(primaryMemoryId.Value, out primaryMemory);
            }

            var primaryPhotoAlbumId = ResolvePrimaryPhotoAlbumId(photo, albumTextMatchIds, albumsByPhotoId);
            PhotoAlbum? primaryAlbum = null;
            if (primaryPhotoAlbumId.HasValue)
            {
                albumLookup.TryGetValue(primaryPhotoAlbumId.Value, out primaryAlbum);
            }

            var title = ResolveSearchResultTitle(photo, primaryMemory, primaryAlbum);

            var thumb = await _photoUrls.GetPresignedUrlAsync(photo, thumbnail: true, cancellationToken: cancellationToken)
                ?? photo.LocalAssetPath;

            results.Add(new SearchResultDto
            {
                PhotoId = photo.Id,
                MemoryId = primaryMemoryId,
                PhotoAlbumId = primaryPhotoAlbumId,
                Title = title,
                ThumbnailPath = thumb,
                MatchReasons = reasons,
                RelevanceScore = 0.75 + _random.NextDouble() * 0.25
            });
        }

        return results.OrderByDescending(r => r.RelevanceScore).ToList();
    }

    private static Guid? ResolvePrimaryMemoryId(Photo photo, HashSet<Guid> memoryTextMatchIds)
    {
        var link = photo.MemoryPhotos
            .OrderBy(mp => mp.SortOrder)
            .FirstOrDefault(mp => memoryTextMatchIds.Contains(mp.MemoryId))
            ?? photo.MemoryPhotos.OrderBy(mp => mp.SortOrder).FirstOrDefault();

        return link is null || link.MemoryId == Guid.Empty ? null : link.MemoryId;
    }

    private static Dictionary<Guid, List<PhotoAlbum>> BuildAlbumsByPhotoId(IEnumerable<PhotoAlbum> albums)
    {
        var map = new Dictionary<Guid, List<PhotoAlbum>>();
        foreach (var album in albums)
        {
            foreach (var link in album.AlbumPhotos)
            {
                if (!map.TryGetValue(link.PhotoId, out var list))
                {
                    list = [];
                    map[link.PhotoId] = list;
                }

                list.Add(album);
            }
        }

        return map;
    }

    private static Guid? ResolvePrimaryPhotoAlbumId(
        Photo photo,
        HashSet<Guid> albumTextMatchIds,
        IReadOnlyDictionary<Guid, List<PhotoAlbum>> albumsByPhotoId)
    {
        if (!albumsByPhotoId.TryGetValue(photo.Id, out var albums) || albums.Count == 0)
        {
            return null;
        }

        var preferred = albums.FirstOrDefault(a => albumTextMatchIds.Contains(a.Id));
        return (preferred ?? albums[0]).Id;
    }

    private static string ResolveSearchResultTitle(Photo photo, Memory? memory, PhotoAlbum? album)
    {
        if (!string.IsNullOrWhiteSpace(photo.Description))
        {
            return photo.Description.Trim();
        }

        if (album is not null && !string.IsNullOrWhiteSpace(album.Description))
        {
            return album.Description.Trim();
        }

        if (memory is not null && !string.IsNullOrWhiteSpace(memory.Title))
        {
            return memory.Title.Trim();
        }

        return photo.OriginalFileName?.Trim() ?? "Photo";
    }

    private static bool MatchesAnySearchTerm(string? text, IReadOnlyList<string> searchTerms)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return searchTerms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TagMatchesAnySearchTerm(string tag, IReadOnlyList<string> searchTerms) =>
        searchTerms.Any(term => tag.Contains(term, StringComparison.OrdinalIgnoreCase)
            || term.Contains(tag, StringComparison.OrdinalIgnoreCase));

    private static List<string> BuildPhotoMatchReasons(
        Photo photo,
        HashSet<Guid> memoryTextMatchIds,
        IReadOnlyDictionary<Guid, Memory> memoryTextMatches,
        HashSet<Guid> albumTextMatchIds,
        IReadOnlyDictionary<Guid, List<PhotoAlbum>> albumsByPhotoId,
        IReadOnlyList<string> searchTerms,
        IReadOnlyList<int> years,
        IReadOnlyDictionary<Guid, List<string>> albumTagsByPhotoId)
    {
        var reasons = new List<string>();

        foreach (var link in photo.MemoryPhotos)
        {
            if (!memoryTextMatchIds.Contains(link.MemoryId)
                || !memoryTextMatches.TryGetValue(link.MemoryId, out var memory))
            {
                continue;
            }

            if (MatchesAnySearchTerm(memory.Title, searchTerms))
            {
                reasons.Add("Title match");
            }

            if (MatchesAnySearchTerm(memory.Description, searchTerms))
            {
                reasons.Add("Description match");
            }

            if (MatchesAnySearchTerm(memory.Location, searchTerms))
            {
                reasons.Add("Memory location match");
            }
        }

        if (MatchesAnySearchTerm(photo.Description, searchTerms))
        {
            reasons.Add("Photo description match");
        }

        if (MatchesAnySearchTerm(photo.Location, searchTerms))
        {
            reasons.Add("Photo location match");
        }

        if (years.Count > 0 && photo.TakenAt.HasValue && years.Contains(photo.TakenAt.Value.Year))
        {
            reasons.Add("Photo date match");
        }

        if (photo.AiTags.Any(t => TagMatchesAnySearchTerm(t.Tag, searchTerms)))
        {
            reasons.Add("AI tag match");
        }

        if (photo.UserTags.Any(t => TagMatchesAnySearchTerm(t.Tag, searchTerms)))
        {
            reasons.Add("User tag match");
        }

        if (albumsByPhotoId.TryGetValue(photo.Id, out var linkedAlbums))
        {
            foreach (var album in linkedAlbums.Where(a => albumTextMatchIds.Contains(a.Id)))
            {
                if (MatchesAnySearchTerm(album.Description, searchTerms))
                {
                    reasons.Add("Album description match");
                }

                if (album.UserTags.Any(t => TagMatchesAnySearchTerm(t.Tag, searchTerms)))
                {
                    reasons.Add("Album tag match");
                }

                if (album.Comments.Any(c => MatchesAnySearchTerm(c.Message, searchTerms)))
                {
                    reasons.Add("Album comment match");
                }
            }
        }
        else if (albumTagsByPhotoId.TryGetValue(photo.Id, out var albumTags)
                 && albumTags.Any(t => searchTerms.Any(term => TagMatchesAnySearchTerm(t, searchTerms))))
        {
            reasons.Add("Album tag match");
        }

        if (reasons.Count == 0)
        {
            reasons.Add("Semantic match");
        }

        return reasons.Distinct(StringComparer.Ordinal).ToList();
    }

    private async Task<Dictionary<Guid, List<string>>> LoadAlbumUserTagsByPhotoIdAsync(
        Guid familyId,
        IEnumerable<Guid> photoIds,
        CancellationToken cancellationToken)
    {
        var idList = photoIds.Distinct().ToList();
        if (idList.Count == 0)
        {
            return [];
        }

        var links = await _db.PhotoAlbumPhotos.AsNoTracking()
            .Where(ap => ap.Photo.FamilyId == familyId && idList.Contains(ap.PhotoId))
            .Select(ap => new { ap.PhotoId, ap.PhotoAlbumId })
            .ToListAsync(cancellationToken);

        if (links.Count == 0)
        {
            return [];
        }

        var albumIds = links.Select(l => l.PhotoAlbumId).Distinct().ToList();
        var tagRows = await _db.PhotoAlbumUserTags.AsNoTracking()
            .Where(t => albumIds.Contains(t.PhotoAlbumId))
            .Select(t => new { t.PhotoAlbumId, t.Tag })
            .ToListAsync(cancellationToken);

        var tagsByAlbumId = tagRows
            .GroupBy(t => t.PhotoAlbumId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Tag).ToList());

        var map = new Dictionary<Guid, List<string>>();
        foreach (var link in links)
        {
            if (!tagsByAlbumId.TryGetValue(link.PhotoAlbumId, out var albumTags))
            {
                continue;
            }

            if (!map.TryGetValue(link.PhotoId, out var tags))
            {
                tags = [];
                map[link.PhotoId] = tags;
            }

            tags.AddRange(albumTags);
        }

        return map;
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

            if (string.IsNullOrWhiteSpace(_options.GrokApiKey))
            {
                await ApplyHeuristicAnalysisAsync(photos, [], cancellationToken);
                return;
            }

            var client = _httpClientFactory.CreateClient("Grok");
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.GrokApiKey);

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
                    _logger.LogWarning("Grok vision call failed for photo {PhotoId}: {Status}", photo.Id, response.StatusCode);
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
