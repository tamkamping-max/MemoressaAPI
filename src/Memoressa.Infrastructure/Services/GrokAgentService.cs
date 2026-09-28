using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Memoressa.Application.Abstractions;
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

    public GrokAgentService(
        IMemoressaDbContext db,
        IOptions<AiOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<GrokAgentService> logger,
        IAiOrchestrationService aiOrchestration,
        IPhotoUrlResolver photoUrls)
    {
        _db = db;
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _aiOrchestration = aiOrchestration;
        _photoUrls = photoUrls;
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
        var searchResults = await _aiOrchestration.SearchMemoriesAsync(familyId, message, cancellationToken);
        var memoryContext = await BuildMemoryContextAsync(familyId, searchResults, cancellationToken);

        var llmResult = string.IsNullOrWhiteSpace(_options.GrokApiKey)
            ? BuildFallbackResponse(message, request.Locale, searchResults)
            : await CallGrokAsync(message, request.Locale, history, memoryContext, cancellationToken);

        llmResult = await ValidateAndEnrichAsync(familyId, llmResult, searchResults, cancellationToken);

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
            MatchReasonKeysJson = JsonSerializer.Serialize(llmResult.MatchReasonKeys)
        });

        session.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return llmResult with { SessionId = session.Id, RelatedMemories = searchResults.Take(5).ToList() };
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
            return "No matching family memories were found in the database for this query.";
        }

        var memoryIds = searchResults
            .Take(_options.AgentMaxContextMemories)
            .Select(r => r.MemoryId)
            .ToList();

        var memories = await _db.Memories.AsNoTracking()
            .Where(m => m.FamilyId == familyId && memoryIds.Contains(m.Id))
            .Include(m => m.MemoryPhotos)
            .Include(m => m.MemoryMembers)
            .ThenInclude(mm => mm.FamilyMember)
            .ToListAsync(cancellationToken);

        var lines = new List<string>();
        foreach (var result in searchResults.Take(_options.AgentMaxContextMemories))
        {
            var memory = memories.FirstOrDefault(m => m.Id == result.MemoryId);
            if (memory is null)
            {
                continue;
            }

            var photoId = memory.MemoryPhotos.OrderBy(mp => mp.SortOrder).Select(mp => mp.PhotoId).FirstOrDefault();
            var members = string.Join(", ", memory.MemoryMembers.Select(mm => mm.FamilyMember.Name));
            string? photoTakenAt = null;
            string? photoLocation = null;
            if (photoId != Guid.Empty)
            {
                var photo = await _db.Photos.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == photoId && p.FamilyId == familyId, cancellationToken);
                if (photo is not null)
                {
                    photoTakenAt = photo.TakenAt?.ToString("yyyy-MM-dd");
                    photoLocation = photo.Location;
                }
            }

            lines.Add(
                $"- memoryId={memory.Id}; photoId={(photoId == Guid.Empty ? "null" : photoId)}; title={memory.Title}; " +
                $"description={memory.Description}; location={memory.Location}; photoTakenAt={photoTakenAt}; photoLocation={photoLocation}; " +
                $"startDate={memory.StartDate:yyyy-MM-dd}; eventType={memory.EventType}; members={members}; " +
                $"matchReasons={string.Join('|', result.MatchReasons)}; score={result.RelevanceScore:F2}");
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
                $"Family memory candidates:\n{memoryContext}\n\nUser question:\n{userMessage}\n\n" +
                "Respond using the required JSON schema."
        });

        var payload = new
        {
            model = _options.ChatModel,
            messages,
            temperature = 0.4,
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
                    ? "我暂时没有在家庭记忆里找到相关的照片，你可以换个说法再试试。"
                    : "I couldn't find matching family memories yet. Try asking in another way.",
                MatchReasonKeys = ["semantic"]
            };
        }

        var top = searchResults[0];
        var reply = IsChineseLocale(locale)
            ? $"我找到了与「{userMessage}」相关的记忆：{top.Title}。"
            : $"I found a memory related to \"{userMessage}\": {top.Title}.";

        return new AiAgentChatResponseDto
        {
            Reply = reply,
            MemoryId = top.MemoryId,
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

    private static string BuildSystemPrompt(string? locale)
    {
        var language = IsChineseLocale(locale)
            ? "Reply in the same language as the user (Chinese when appropriate)."
            : "Reply in the same language as the user.";

        return """
            You are Memoressa, a warm family memory assistant. Help the user find and talk about their family photos and memories.
            Use ONLY the provided family memory candidates. Do not invent memories, people, or dates.
            Return JSON with this exact shape:
            {
              "reply": "natural conversational answer for the user",
              "memoryId": "uuid or null",
              "photoId": "uuid or null",
              "matchReasonKeys": ["semantic", "familyRelation"]
            }
            Allowed matchReasonKeys: familyRelation, faceMatch, summer2018, birthdayEvent, childGrowth, familyGathering, locationTokyo, travelEvent, multiGeneration, semantic, aiCurated, highEmotional.
            Pick at most 3 matchReasonKeys. Prefer the best matching memoryId/photoId from the candidates when relevant.
            """ + language;
    }

    private static bool IsChineseLocale(string? locale) =>
        locale?.StartsWith("zh", StringComparison.OrdinalIgnoreCase) == true;

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
