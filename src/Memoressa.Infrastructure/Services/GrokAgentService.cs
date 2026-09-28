using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Memoressa.Application.Abstractions;
using Memoressa.Application.Common;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Domain.Entities;
using Memoressa.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Memoressa.Infrastructure.Services;

public class GrokAgentService : IGrokAgentService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IMemoressaDbContext _db;
    private readonly AiOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GrokAgentService> _logger;
    private readonly IAiOrchestrationService _aiOrchestration;
    private readonly IPhotoUrlResolver _photoUrls;
    private readonly IUploadService _uploads;

    public GrokAgentService(
        IMemoressaDbContext db,
        IOptions<AiOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<GrokAgentService> logger,
        IAiOrchestrationService aiOrchestration,
        IPhotoUrlResolver photoUrls,
        IUploadService uploads)
    {
        _db = db;
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _aiOrchestration = aiOrchestration;
        _photoUrls = photoUrls;
        _uploads = uploads;
    }

    public async Task<AiAgentChatResponseDto> ChatAsync(
        Guid userId,
        Guid familyId,
        AiAgentChatRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var message = request.Message.Trim();
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new Application.Common.ApiException("Message is required", 400);
        }

        var session = await ResolveSessionAsync(userId, familyId, request.SessionId, cancellationToken);
        var history = await LoadRecentMessagesAsync(session.Id, cancellationToken);
        var intent = AgentIntentDetector.Detect(message);
        var isStructuredIntent = intent.Kind != AgentIntentKind.None;
        var expandedKeywords = isStructuredIntent
            ? Array.Empty<string>()
            : await TryExpandSearchKeywordsWithGrokAsync(message, request.Locale, cancellationToken);

        IReadOnlyList<SearchResultDto> searchResults;
        string memoryContext;

        (searchResults, memoryContext) = await BuildIntentContextAsync(
            familyId,
            intent,
            message,
            expandedKeywords,
            cancellationToken);

        var hasGrokKey = !string.IsNullOrWhiteSpace(_options.GrokApiKey);
        var useGrok = hasGrokKey
            && (isStructuredIntent
                || !(_options.AgentSkipGrokForSimpleSearch && AgentSearchQuery.IsSimpleSearchPhrase(message)));

        AiAgentChatResponseDto llmResult;
        if (!useGrok)
        {
            llmResult = await TryBuildStructuredFallbackAsync(
                familyId,
                intent,
                request.Locale,
                searchResults,
                cancellationToken)
                ?? BuildFallbackResponse(message, request.Locale, searchResults);
        }
        else
        {
            llmResult = await CallGrokAsync(message, request.Locale, history, memoryContext, cancellationToken);
        }

        llmResult = await ValidateAndEnrichAsync(familyId, llmResult, searchResults, cancellationToken);

        var relatedForChat = searchResults
            .Take(_options.AgentMaxRelatedPhotosInChat)
            .ToList();

        _db.AiChatMessages.Add(new AiChatMessage
        {
            SessionId = session.Id,
            Role = "user",
            Content = message
        });

        _db.AiChatMessages.Add(new AiChatMessage
        {
            SessionId = session.Id,
            Role = "assistant",
            Content = llmResult.Reply,
            MemoryId = llmResult.MemoryId,
            PhotoId = llmResult.PhotoId,
            MatchReasonKeysJson = JsonSerializer.Serialize(llmResult.MatchReasonKeys),
            RelatedPhotoIdsJson = JsonSerializer.Serialize(relatedForChat.Select(r => r.PhotoId).ToList())
        });

        session.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return llmResult with { SessionId = session.Id, RelatedMemories = relatedForChat };
    }

    private async Task<(IReadOnlyList<SearchResultDto> Results, string Context)> BuildIntentContextAsync(
        Guid familyId,
        AgentIntent intent,
        string message,
        IReadOnlyList<string> expandedKeywords,
        CancellationToken cancellationToken)
    {
        switch (intent.Kind)
        {
            case AgentIntentKind.CountPhotos:
            {
                var count = await _aiOrchestration.CountVisiblePhotosAsync(familyId, cancellationToken);
                return ([], $"Library stats: visible_photo_count={count}. User asked total photo count — use this number exactly.");
            }
            case AgentIntentKind.ListPhotos:
            {
                var total = await _aiOrchestration.CountVisiblePhotosAsync(familyId, cancellationToken);
                var results = await _aiOrchestration.ListVisiblePhotosForAgentAsync(
                    familyId,
                    _options.AgentMaxRelatedPhotosInChat,
                    cancellationToken);
                var browseContext = await BuildMemoryContextAsync(familyId, results, cancellationToken);
                return (results,
                    $"Library stats: visible_photo_count={total}; showing_most_recent={results.Count}. " +
                    "User asked to browse photos — chat shows recent thumbnails; Timeline has the full library.\n" +
                    browseContext);
            }
            case AgentIntentKind.CountRecentPhotos:
            {
                var since = DateTime.UtcNow.AddDays(-intent.RecentDays);
                var count = await _aiOrchestration.CountVisiblePhotosSinceAsync(familyId, since, cancellationToken);
                return ([], $"Library stats: recent_day_window={intent.RecentDays}; visible_photo_count_in_window={count}. Use these facts exactly.");
            }
            case AgentIntentKind.ListRecentPhotos:
            {
                var since = DateTime.UtcNow.AddDays(-intent.RecentDays);
                var total = await _aiOrchestration.CountVisiblePhotosSinceAsync(familyId, since, cancellationToken);
                var results = await _aiOrchestration.ListVisiblePhotosSinceAsync(
                    familyId,
                    since,
                    _options.AgentMaxRelatedPhotosInChat,
                    cancellationToken);
                var browseContext = await BuildMemoryContextAsync(familyId, results, cancellationToken);
                return (results,
                    $"Library stats: recent_day_window={intent.RecentDays}; visible_photo_count_in_window={total}; showing_in_chat={results.Count}.\n" +
                    browseContext);
            }
            case AgentIntentKind.StorageUsage:
            {
                var storage = await _uploads.GetStorageUsageAsync(cancellationToken);
                if (!storage.Success || storage.Data is null)
                {
                    return ([], "Storage stats: unavailable (could not load quota).");
                }

                var used = storage.Data.UsedBytes;
                var limit = storage.Data.LimitBytes;
                var pct = limit > 0 ? Math.Round(100.0 * used / limit, 1) : 0;
                return ([], $"Storage stats: used_bytes={used}; limit_bytes={limit}; used_percent={pct}. Explain clearly in user's language.");
            }
            case AgentIntentKind.UploadStatus:
            {
                var uploads = await _uploads.GetIncompleteUploadsAsync(cancellationToken);
                if (!uploads.Success || uploads.Data is null)
                {
                    return ([], "Upload stats: unavailable (could not load sessions).");
                }

                var list = uploads.Data;
                var names = string.Join(", ", list.Take(5).Select(u => u.FileName));
                return ([], $"Upload stats: incomplete_count={list.Count}; sample_file_names={names}. Mention Upload queue in app if helpful.");
            }
            case AgentIntentKind.Help:
                return ([], "User asked what the agent can do. Capabilities: search photos by tag/person/place/date; count or browse library; recent N days; storage quota; incomplete uploads; multi-turn chat.");
            default:
            {
                var results = await _aiOrchestration.SearchMemoriesAsync(
                    familyId,
                    message,
                    cancellationToken,
                    expandedKeywords);
                var ctx = await BuildMemoryContextAsync(familyId, results, cancellationToken);
                return (results, ctx);
            }
        }
    }

    private async Task<AiAgentChatResponseDto?> TryBuildStructuredFallbackAsync(
        Guid familyId,
        AgentIntent intent,
        string? locale,
        IReadOnlyList<SearchResultDto> searchResults,
        CancellationToken cancellationToken)
    {
        switch (intent.Kind)
        {
            case AgentIntentKind.None:
                return null;
            case AgentIntentKind.CountPhotos:
            {
                var count = await _aiOrchestration.CountVisiblePhotosAsync(familyId, cancellationToken);
                return BuildPhotoCountResponse(count, locale);
            }
            case AgentIntentKind.CountRecentPhotos:
            {
                var since = DateTime.UtcNow.AddDays(-intent.RecentDays);
                var count = await _aiOrchestration.CountVisiblePhotosSinceAsync(familyId, since, cancellationToken);
                return BuildRecentPhotoCountResponse(count, intent.RecentDays, locale);
            }
            case AgentIntentKind.ListPhotos:
            case AgentIntentKind.ListRecentPhotos:
                return BuildBrowseFallbackResponse(intent, locale, searchResults);
            case AgentIntentKind.StorageUsage:
            {
                var storage = await _uploads.GetStorageUsageAsync(cancellationToken);
                if (!storage.Success || storage.Data is null)
                {
                    return BuildUnavailableStatsResponse(locale, isStorage: true);
                }

                return BuildStorageUsageResponse(storage.Data, locale);
            }
            case AgentIntentKind.UploadStatus:
            {
                var uploads = await _uploads.GetIncompleteUploadsAsync(cancellationToken);
                if (!uploads.Success || uploads.Data is null)
                {
                    return BuildUnavailableStatsResponse(locale, isStorage: false);
                }

                return BuildUploadStatusResponse(uploads.Data, locale);
            }
            case AgentIntentKind.Help:
                return BuildHelpResponse(locale);
            default:
                return null;
        }
    }

    private async Task<AiChatSession> ResolveSessionAsync(
        Guid userId,
        Guid familyId,
        Guid? sessionId,
        CancellationToken cancellationToken)
    {
        if (sessionId.HasValue)
        {
            var existing = await _db.AiChatSessions
                .FirstOrDefaultAsync(
                    s => s.Id == sessionId.Value && s.UserId == userId && s.FamilyId == familyId,
                    cancellationToken);

            if (existing is not null)
            {
                return existing;
            }
        }

        var latest = await _db.AiChatSessions
            .Where(s => s.UserId == userId && s.FamilyId == familyId)
            .OrderByDescending(s => s.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latest is not null)
        {
            return latest;
        }

        var session = new AiChatSession
        {
            UserId = userId,
            FamilyId = familyId
        };
        _db.AiChatSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);
        return session;
    }

    private async Task<List<AiChatMessage>> LoadRecentMessagesAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var recentIds = await _db.AiChatMessages.AsNoTracking()
            .Where(m => m.SessionId == sessionId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(_options.AgentMaxHistoryMessages)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);

        if (recentIds.Count == 0)
        {
            return [];
        }

        return await _db.AiChatMessages.AsNoTracking()
            .Where(m => recentIds.Contains(m.Id))
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    private async Task<string> BuildMemoryContextAsync(
        Guid familyId,
        IReadOnlyList<SearchResultDto> searchResults,
        CancellationToken cancellationToken)
    {
        if (searchResults.Count == 0)
        {
            return "(Search returned no photo candidates for this query.)";
        }

        var topResults = searchResults.Take(_options.AgentMaxContextMemories).ToList();
        var photoIds = topResults.Select(r => r.PhotoId).ToList();
        var memoryIds = topResults.Where(r => r.MemoryId.HasValue).Select(r => r.MemoryId!.Value).Distinct().ToList();
        var albumIds = topResults.Where(r => r.PhotoAlbumId.HasValue).Select(r => r.PhotoAlbumId!.Value).Distinct().ToList();

        var photos = await _db.Photos.AsNoTracking()
            .Where(p => p.FamilyId == familyId && photoIds.Contains(p.Id))
            .Include(p => p.UserTags)
            .Include(p => p.AiTags)
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var memories = memoryIds.Count == 0
            ? new Dictionary<Guid, Memory>()
            : await _db.Memories.AsNoTracking()
                .Where(m => m.FamilyId == familyId && memoryIds.Contains(m.Id))
                .ToDictionaryAsync(m => m.Id, cancellationToken);

        var albums = albumIds.Count == 0
            ? new Dictionary<Guid, PhotoAlbum>()
            : await _db.PhotoAlbums.AsNoTracking()
                .Where(a => a.FamilyId == familyId && albumIds.Contains(a.Id))
                .Include(a => a.UserTags)
                .ToDictionaryAsync(a => a.Id, cancellationToken);

        var lines = new List<string>();
        foreach (var result in topResults)
        {
            if (!photos.TryGetValue(result.PhotoId, out var photo))
            {
                continue;
            }

            Memory? memory = null;
            if (result.MemoryId.HasValue)
            {
                memories.TryGetValue(result.MemoryId.Value, out memory);
            }

            PhotoAlbum? album = null;
            if (result.PhotoAlbumId.HasValue)
            {
                albums.TryGetValue(result.PhotoAlbumId.Value, out album);
            }

            var albumTags = album is null
                ? string.Empty
                : string.Join(", ", album.UserTags.Select(t => t.Tag));

            var photoTags = string.Join(
                ", ",
                photo.UserTags.Select(t => t.Tag).Concat(photo.AiTags.Select(t => t.Tag)).Take(6));

            lines.Add(
                $"- pid={photo.Id}; mid={result.MemoryId}; aid={result.PhotoAlbumId}; " +
                $"why={string.Join('|', result.MatchReasons)}; " +
                $"cap={TrimContextField(photo.Description ?? memory?.Title)}; " +
                $"loc={TrimContextField(photo.Location ?? memory?.Location)}; " +
                $"date={photo.TakenAt:yyyy-MM-dd}; tags={TrimContextField(photoTags)}; " +
                $"albumTags={TrimContextField(albumTags)}");
        }

        return string.Join('\n', lines);
    }

    private async Task<AiAgentChatResponseDto> CallGrokAsync(
        string userMessage,
        string? locale,
        IReadOnlyList<AiChatMessage> history,
        string memoryContext,
        CancellationToken cancellationToken)
    {
        var systemPrompt = BuildSystemPrompt(locale);
        var messages = new List<object> { new { role = "system", content = systemPrompt } };

        foreach (var item in history)
        {
            if (item.Role is "user" or "assistant")
            {
                messages.Add(new { role = item.Role, content = item.Content });
            }
        }

        messages.Add(new
        {
            role = "user",
            content =
                $"Family photo candidates from search (may be empty):\n{memoryContext}\n\n" +
                $"User message:\n{userMessage}"
        });

        var payload = new
        {
            model = _options.ChatModel,
            messages,
            temperature = 0.65,
            max_tokens = _options.AgentMaxCompletionTokens,
            response_format = new { type = "json_object" }
        };

        var client = _httpClientFactory.CreateClient("Grok");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.GrokApiKey);

        using var response = await client.PostAsJsonAsync("chat/completions", payload, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Grok chat failed: {Status} {Body}", response.StatusCode, errorBody);
            throw new Application.Common.ApiException("AI agent is temporarily unavailable", 503);
        }

        var body = await response.Content.ReadFromJsonAsync<GrokChatCompletionResponse>(JsonOptions, cancellationToken);
        var content = body?.Choices?.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new Application.Common.ApiException("AI agent returned an empty response", 502);
        }

        var parsed = JsonSerializer.Deserialize<LlmAgentPayload>(content, JsonOptions)
            ?? throw new Application.Common.ApiException("AI agent returned invalid JSON", 502);

        return new AiAgentChatResponseDto
        {
            Reply = parsed.Reply?.Trim() ?? string.Empty,
            MemoryId = parsed.MemoryId,
            PhotoId = parsed.PhotoId,
            MatchReasonKeys = NormalizeMatchReasons(parsed.MatchReasonKeys)
        };
    }

    private static AiAgentChatResponseDto BuildPhotoCountResponse(int count, string? locale)
    {
        var reply = IsChineseLocale(locale)
            ? $"你的家庭相册里目前有 {count} 张未隐藏的照片。想浏览的话可以说「显示最近的照片」，或到时间轴查看全部。"
            : $"You have {count} visible photos in your family library. Say \"show recent photos\" here, or open the timeline for everything.";

        return new AiAgentChatResponseDto
        {
            Reply = reply,
            MatchReasonKeys = ["semantic"]
        };
    }

    private static AiAgentChatResponseDto BuildRecentPhotoCountResponse(int count, int days, string? locale)
    {
        var reply = IsChineseLocale(locale)
            ? $"最近 {days} 天里，家庭相册新增了 {count} 张未隐藏的照片（按拍摄或上传时间）。想直接看缩图可以说「最近{days}天的照片」。"
            : $"In the last {days} days, your family library has {count} visible photos (by taken or upload date). Ask to \"show photos from the last {days} days\" to see thumbnails here.";

        return new AiAgentChatResponseDto
        {
            Reply = reply,
            MatchReasonKeys = ["semantic"]
        };
    }

    private static AiAgentChatResponseDto BuildBrowseFallbackResponse(
        AgentIntent intent,
        string? locale,
        IReadOnlyList<SearchResultDto> searchResults)
    {
        var days = intent.RecentDays;
        var showing = searchResults.Count;
        if (showing == 0)
        {
            var empty = IsChineseLocale(locale)
                ? intent.Kind == AgentIntentKind.ListRecentPhotos
                    ? $"最近 {days} 天里没有找到未隐藏的照片。可以试试拉长时间，或用 tag、地点来搜。"
                    : "相册里还没有可显示的照片，或都被隐藏了。上传后我可以帮你找或整理。"
                : intent.Kind == AgentIntentKind.ListRecentPhotos
                    ? $"No visible photos in the last {days} days. Try a longer window or search by tag or place."
                    : "There are no visible photos to show yet. Upload some and I can help you find them.";

            return new AiAgentChatResponseDto { Reply = empty, MatchReasonKeys = ["semantic"] };
        }

        var top = searchResults[0];
        var reply = IsChineseLocale(locale)
            ? intent.Kind == AgentIntentKind.ListRecentPhotos
                ? $"最近 {days} 天里有 {showing} 张缩图在下方（聊天最多显示这么多张）。点图查看详情，或到时间轴看完整列表。"
                : $"下面是最新的 {showing} 张缩图（聊天里一次看不全库）。需要全部请打开时间轴，或告诉我更具体的条件。"
            : intent.Kind == AgentIntentKind.ListRecentPhotos
                ? $"Here are {showing} thumbnails from the last {days} days (chat shows up to this many). Tap to open, or use Timeline for the full set."
                : $"Here are the {showing} most recent thumbnails (not the entire library). Open Timeline for everything, or narrow your question.";

        return new AiAgentChatResponseDto
        {
            Reply = reply,
            MemoryId = top.MemoryId,
            PhotoId = top.PhotoId,
            MatchReasonKeys = ["semantic"]
        };
    }

    private static AiAgentChatResponseDto BuildStorageUsageResponse(StorageUsageDto usage, string? locale)
    {
        var used = FormatBytes(usage.UsedBytes, locale);
        var limit = FormatBytes(usage.LimitBytes, locale);
        var pct = usage.LimitBytes > 0
            ? Math.Round(100.0 * usage.UsedBytes / usage.LimitBytes, 1)
            : 0;

        var reply = IsChineseLocale(locale)
            ? $"云端存储已用 {used} / {limit}（约 {pct}%）。若要腾空间，可以删除不需要的照片，或先处理未完成的上传。"
            : $"Cloud storage: {used} of {limit} used (~{pct}%). Delete photos you no longer need, or finish pending uploads to free quota.";

        return new AiAgentChatResponseDto { Reply = reply, MatchReasonKeys = ["semantic"] };
    }

    private static AiAgentChatResponseDto BuildUploadStatusResponse(
        IReadOnlyList<IncompleteUploadSessionDto> sessions,
        string? locale)
    {
        if (sessions.Count == 0)
        {
            var none = IsChineseLocale(locale)
                ? "目前没有未完成的上传，一切都已经同步好了。"
                : "You have no incomplete uploads — everything looks synced.";
            return new AiAgentChatResponseDto { Reply = none, MatchReasonKeys = ["semantic"] };
        }

        var sample = string.Join(
            IsChineseLocale(locale) ? "、" : ", ",
            sessions.Take(3).Select(s => s.FileName));

        var reply = IsChineseLocale(locale)
            ? $"你有 {sessions.Count} 个上传还没完成（例如：{sample}）。请到 App 的上传队列继续传完，否则会占用存储配额。"
            : $"You have {sessions.Count} upload(s) still in progress (e.g. {sample}). Open the upload queue in the app to finish them so quota is released.";

        return new AiAgentChatResponseDto { Reply = reply, MatchReasonKeys = ["semantic"] };
    }

    private static AiAgentChatResponseDto BuildHelpResponse(string? locale)
    {
        var reply = IsChineseLocale(locale)
            ? "我是 Memoressa 家庭回忆助手。你可以：用 tag、人名、地点或「今年9月」搜照片；问「有多少张」或「最近7天有几张」；说「显示最近照片」看缩图；问「存储空间」或「上传进度」。我会记住这段对话，越问越准。"
            : "I'm Memoressa, your family memory assistant. Search by tag, name, place, or month; ask photo counts or \"last 7 days\"; browse recent thumbnails; check storage or upload progress. I keep chat context so follow-ups work naturally.";

        return new AiAgentChatResponseDto { Reply = reply, MatchReasonKeys = ["semantic"] };
    }

    private static AiAgentChatResponseDto BuildUnavailableStatsResponse(string? locale, bool isStorage)
    {
        var reply = IsChineseLocale(locale)
            ? isStorage
                ? "暂时读不到存储用量，请稍后再试，或在 App 设置里查看云端空间。"
                : "暂时读不到上传进度，请稍后再试，或在 App 的上传队列查看。"
            : isStorage
                ? "I couldn't load storage usage right now. Try again later or check cloud storage in Settings."
                : "I couldn't load upload progress right now. Try again later or check the upload queue in the app.";

        return new AiAgentChatResponseDto { Reply = reply, MatchReasonKeys = ["semantic"] };
    }

    private static string FormatBytes(long bytes, string? locale)
    {
        const double kb = 1024;
        const double mb = kb * 1024;
        const double gb = mb * 1024;

        if (bytes >= gb)
        {
            return IsChineseLocale(locale)
                ? $"{bytes / gb:0.##} GB"
                : $"{bytes / gb:0.##} GB";
        }

        if (bytes >= mb)
        {
            return $"{bytes / mb:0.#} MB";
        }

        if (bytes >= kb)
        {
            return $"{bytes / kb:0.#} KB";
        }

        return $"{bytes} B";
    }

    private static AiAgentChatResponseDto BuildFallbackResponse(
        string userMessage,
        string? locale,
        IReadOnlyList<SearchResultDto> searchResults)
    {
        if (searchResults.Count == 0)
        {
            return new AiAgentChatResponseDto
            {
                Reply = IsChineseLocale(locale)
                    ? "我在你的家庭相册里还没找到符合的照片。你可以试试换个关键词（人名、地点、tag），或加上月份，例如「今年9月的照片」。"
                    : "I couldn't find matching photos in your family library yet. Try another keyword (name, place, tag) or add a month, e.g. \"photos from September this year\".",
                MatchReasonKeys = ["semantic"]
            };
        }

        var top = searchResults[0];
        var count = searchResults.Count;
        var reply = IsChineseLocale(locale)
            ? count == 1
                ? $"我找到了 1 张可能相关的照片（见下方缩图）。如果想看更多，可以再说具体一点，例如年份、地点或 tag。"
                : $"我找到了 {count} 张可能相关的照片（见下方缩图）。你可以点图查看，或继续问我更细的条件。"
            : count == 1
                ? $"I found 1 matching photo (thumbnail below). Ask with more detail if you need a different one."
                : $"I found {count} matching photos (thumbnails below). Tap to open, or refine your question.";

        return new AiAgentChatResponseDto
        {
            Reply = reply,
            MemoryId = top.MemoryId,
            PhotoId = top.PhotoId,
            MatchReasonKeys = NormalizeMatchReasons(top.MatchReasons)
        };
    }

    private async Task<AiAgentChatResponseDto> ValidateAndEnrichAsync(
        Guid familyId,
        AiAgentChatResponseDto response,
        IReadOnlyList<SearchResultDto> searchResults,
        CancellationToken cancellationToken)
    {
        Guid? memoryId = response.MemoryId;
        Guid? photoId = response.PhotoId;

        if (memoryId.HasValue)
        {
            var memoryExists = await _db.Memories.AsNoTracking()
                .AnyAsync(m => m.Id == memoryId.Value && m.FamilyId == familyId, cancellationToken);
            if (!memoryExists)
            {
                memoryId = null;
                photoId = null;
            }
        }

        if (memoryId is null && searchResults.Count > 0)
        {
            memoryId = searchResults[0].MemoryId;
        }

        if (!photoId.HasValue && searchResults.Count > 0)
        {
            photoId = searchResults[0].PhotoId;
        }

        if (memoryId.HasValue && !photoId.HasValue)
        {
            photoId = await _db.MemoryPhotos.AsNoTracking()
                .Where(mp => mp.MemoryId == memoryId.Value)
                .OrderBy(mp => mp.SortOrder)
                .Select(mp => mp.PhotoId)
                .FirstOrDefaultAsync(cancellationToken);

            if (photoId == Guid.Empty)
            {
                photoId = null;
            }
        }

        if (photoId.HasValue)
        {
            var photo = await _db.Photos.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == photoId.Value && p.FamilyId == familyId, cancellationToken);

            if (photo is null)
            {
                photoId = null;
            }
            else
            {
                var thumb = await _photoUrls.GetPresignedUrlAsync(photo, thumbnail: true, cancellationToken: cancellationToken);
                return response with
                {
                    MemoryId = memoryId,
                    PhotoId = photoId,
                    ThumbnailUrl = thumb
                };
            }
        }

        if (searchResults.Count > 0 && !string.IsNullOrWhiteSpace(searchResults[0].ThumbnailPath))
        {
            return response with
            {
                MemoryId = memoryId,
                ThumbnailUrl = searchResults[0].ThumbnailPath
            };
        }

        return response with { MemoryId = memoryId, PhotoId = photoId };
    }

    private async Task<IReadOnlyList<string>> TryExpandSearchKeywordsWithGrokAsync(
        string userMessage,
        string? locale,
        CancellationToken cancellationToken)
    {
        if (!_options.AgentGrokSearchExpansion || string.IsNullOrWhiteSpace(_options.GrokApiKey))
        {
            return [];
        }

        var payload = new
        {
            model = _options.ChatModel,
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content =
                        "You expand user questions into short keywords for substring search on family photo tags and captions. " +
                        "The app supports multiple UI locales (e.g. zh-TW, zh-CN, en, ja, ko, es, fr, de). " +
                        "Tags may be stored in any language. Include the user's words plus likely tag spellings and translations " +
                        $"(max {_options.AgentMaxSearchKeywords} keywords, each ≤128 chars). Return JSON only."
                },
                new
                {
                    role = "user",
                    content =
                        $"User locale: {locale ?? "unknown"}\nUser message: {userMessage}\n" +
                        "Return: {\"keywords\":[\"...\"]}"
                }
            },
            temperature = 0,
            max_tokens = 256,
            response_format = new { type = "json_object" }
        };

        try
        {
            var client = _httpClientFactory.CreateClient("Grok");
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.GrokApiKey);

            using var response = await client.PostAsJsonAsync("chat/completions", payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Grok search keyword expansion failed: {Status}", response.StatusCode);
                return [];
            }

            var body = await response.Content.ReadFromJsonAsync<GrokChatCompletionResponse>(JsonOptions, cancellationToken);
            var content = body?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrWhiteSpace(content))
            {
                return [];
            }

            var parsed = JsonSerializer.Deserialize<SearchKeywordPayload>(content, JsonOptions);
            if (parsed?.Keywords is null || parsed.Keywords.Count == 0)
            {
                return [];
            }

            return parsed.Keywords
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Select(k => k.Trim())
                .Where(k => k.Length <= AgentSearchTermBuilder.MaxKeywordLength)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(_options.AgentMaxSearchKeywords)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Grok search keyword expansion error");
            return [];
        }
    }

    private static string BuildSystemPrompt(string? locale)
    {
        var language = IsChineseLocale(locale)
            ? "Reply in the same language as the user (Traditional/Simplified Chinese as appropriate)."
            : "Reply in the same language as the user.";

        return """
            You are Memoressa, a warm family memory curator inside a photo app — conversational, helpful, and concise.
            The user is chatting with you; treat each turn as part of an ongoing dialogue (use recent history).
            Use the search candidate list when it helps; never invent photos, people, dates, or events not supported by candidates.
            When the context includes Library stats (visible_photo_count, recent_day_window, showing_most_recent) or Storage/Upload stats (used_bytes, limit_bytes, incomplete_count), use those numbers exactly.
            If candidates are empty, say so kindly and suggest how to rephrase (tags, month, year, place, person).
            If candidates exist, briefly explain what you found and invite follow-up; pick memoryId/photoId from candidates when highlighting one photo.
            Write reply as 2–5 natural sentences (not bullet lists). JSON only:
            {"reply":"","memoryId":null,"photoId":null,"matchReasonKeys":["semantic"]}
            matchReasonKeys (max 3): familyRelation, faceMatch, summer2018, birthdayEvent, childGrowth, familyGathering, locationTokyo, travelEvent, multiGeneration, semantic, aiCurated, highEmotional.
            """ + language;
    }

    private static bool IsChineseLocale(string? locale) =>
        locale?.StartsWith("zh", StringComparison.OrdinalIgnoreCase) == true;

    private static string TrimContextField(string? value, int maxLength = 80)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength] + "…";
    }

    private static IReadOnlyList<string> NormalizeMatchReasons(IEnumerable<string>? keys)
    {
        if (keys is null)
        {
            return ["semantic"];
        }

        var normalized = keys
            .Where(k => AiAgentMatchReasons.AllowedKeys.Contains(k, StringComparer.OrdinalIgnoreCase))
            .Select(k => AiAgentMatchReasons.AllowedKeys.First(a => a.Equals(k, StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.Ordinal)
            .Take(3)
            .ToList();

        return normalized.Count > 0 ? normalized : ["semantic"];
    }

    private sealed class SearchKeywordPayload
    {
        [JsonPropertyName("keywords")] public List<string>? Keywords { get; set; }
    }

    private sealed class LlmAgentPayload
    {
        [JsonPropertyName("reply")] public string? Reply { get; set; }
        [JsonPropertyName("memoryId")] public Guid? MemoryId { get; set; }
        [JsonPropertyName("photoId")] public Guid? PhotoId { get; set; }
        [JsonPropertyName("matchReasonKeys")] public List<string>? MatchReasonKeys { get; set; }
    }

    private sealed class GrokChatCompletionResponse
    {
        [JsonPropertyName("choices")] public List<GrokChoice>? Choices { get; set; }
    }

    private sealed class GrokChoice
    {
        [JsonPropertyName("message")] public GrokMessage? Message { get; set; }
    }

    private sealed class GrokMessage
    {
        [JsonPropertyName("content")] public string? Content { get; set; }
    }
}
