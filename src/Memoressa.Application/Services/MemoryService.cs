using Memoressa.Application.Abstractions;
using Memoressa.Application.Common;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Application.Services;

public class MemoryService : IMemoryService
{
    private readonly IMemoressaDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly Random _random = new();

    public MemoryService(IMemoressaDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ServiceResult<IReadOnlyList<MemoryDto>>> GetMemoriesAsync(CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<IReadOnlyList<MemoryDto>>.Fail("Unauthorized", 401);
        }

        var memories = await QueryMemories(ctx.Value.FamilyId)
            .Where(m => !m.IsTodayHighlight && !m.IsAiGenerated)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<MemoryDto>>.Ok(memories.Select(m => m.ToDto()).ToList());
    }

    public async Task<ServiceResult<IReadOnlyList<MemoryDto>>> GetAiCuratedMemoriesAsync(CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<IReadOnlyList<MemoryDto>>.Fail("Unauthorized", 401);
        }

        var memories = await QueryMemories(ctx.Value.FamilyId)
            .Where(m => m.IsAiGenerated)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<MemoryDto>>.Ok(memories.Select(m => m.ToDto()).ToList());
    }

    public async Task<ServiceResult<IReadOnlyList<MemoryDto>>> GetTodayMemoriesAsync(CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<IReadOnlyList<MemoryDto>>.Fail("Unauthorized", 401);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var cached = await _db.TodayHighlightCaches.AsNoTracking()
            .Include(c => c.Memory)
            .ThenInclude(m => m!.MemoryPhotos)
            .Include(c => c.Memory)
            .ThenInclude(m => m!.MemoryVideos)
            .Include(c => c.Memory)
            .ThenInclude(m => m!.MemoryMembers)
            .FirstOrDefaultAsync(c => c.FamilyId == ctx.Value.FamilyId && c.CacheDate == today, cancellationToken);

        if (cached?.Memory is null)
        {
            return ServiceResult<IReadOnlyList<MemoryDto>>.Ok([]);
        }

        return ServiceResult<IReadOnlyList<MemoryDto>>.Ok([cached.Memory.ToDto()]);
    }

    public async Task<ServiceResult<MemoryDto>> RegenerateTodayHighlightAsync(CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<MemoryDto>.Fail("Unauthorized", 401);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var photos = await _db.Photos
            .Where(p => p.FamilyId == ctx.Value.FamilyId && !p.IsHidden)
            .Include(p => p.PhotoMembers)
            .ToListAsync(cancellationToken);

        if (photos.Count < AppConstants.TodayMemoryMinPhotos)
        {
            return ServiceResult<MemoryDto>.Fail("Not enough photos for today's memory");
        }

        var targetCount = AppConstants.TodayMemoryMinPhotos +
                          _random.Next(AppConstants.TodayMemoryMaxPhotos - AppConstants.TodayMemoryMinPhotos + 1);
        var count = Math.Min(targetCount, photos.Count);
        var selected = photos.OrderBy(_ => _random.Next()).Take(count).OrderBy(p => p.TakenAt).ToList();

        var existing = await _db.TodayHighlightCaches
            .Include(c => c.Memory)
            .ThenInclude(m => m!.MemoryPhotos)
            .FirstOrDefaultAsync(c => c.FamilyId == ctx.Value.FamilyId && c.CacheDate == today, cancellationToken);

        Memory memory;
        if (existing is not null)
        {
            memory = existing.Memory;
            _db.MemoryPhotos.RemoveRange(memory.MemoryPhotos);
            memory.Title = "Today's Memory";
            memory.Description = "AI curated highlight for today";
            memory.IsTodayHighlight = true;
            memory.IsAiGenerated = true;
            memory.StartDate = DateTime.UtcNow;
            memory.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            var oldHighlights = await _db.Memories
                .Where(m => m.FamilyId == ctx.Value.FamilyId && m.IsTodayHighlight)
                .ToListAsync(cancellationToken);
            _db.Memories.RemoveRange(oldHighlights);

            memory = new Memory
            {
                FamilyId = ctx.Value.FamilyId,
                CreatedByUserId = ctx.Value.UserId,
                Title = "Today's Memory",
                Description = "AI curated highlight for today",
                Type = MemoryType.AiMemory,
                IsAiGenerated = true,
                IsTodayHighlight = true,
                StartDate = DateTime.UtcNow,
                Visibility = MemoryVisibility.Family
            };
            _db.Memories.Add(memory);
            _db.TodayHighlightCaches.Add(new TodayHighlightCache
            {
                FamilyId = ctx.Value.FamilyId,
                CacheDate = today,
                MemoryId = memory.Id
            });
        }

        var order = 0;
        foreach (var photo in selected)
        {
            _db.MemoryPhotos.Add(new MemoryPhoto
            {
                MemoryId = memory.Id,
                PhotoId = photo.Id,
                SortOrder = order++
            });
        }

        var memberIds = selected.SelectMany(p => p.PhotoMembers.Select(pm => pm.FamilyMemberId)).Distinct();
        _db.MemoryMembers.RemoveRange(await _db.MemoryMembers.Where(mm => mm.MemoryId == memory.Id).ToListAsync(cancellationToken));
        foreach (var memberId in memberIds)
        {
            _db.MemoryMembers.Add(new MemoryMember { MemoryId = memory.Id, FamilyMemberId = memberId });
        }

        await _db.SaveChangesAsync(cancellationToken);
        await LoadMemoryGraphAsync(memory.Id, cancellationToken);
        var loaded = await QueryMemories(ctx.Value.FamilyId).FirstAsync(m => m.Id == memory.Id, cancellationToken);
        return ServiceResult<MemoryDto>.Ok(loaded.ToDto());
    }

    public async Task<ServiceResult<IReadOnlyList<MemoryDto>>> GetYearsAgoTodayMemoriesAsync(CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<IReadOnlyList<MemoryDto>>.Fail("Unauthorized", 401);
        }

        var today = DateTime.UtcNow;
        var memories = await QueryMemories(ctx.Value.FamilyId)
            .Where(m => m.StartDate.HasValue &&
                        m.StartDate.Value.Month == today.Month &&
                        m.StartDate.Value.Day == today.Day &&
                        m.StartDate.Value.Year < today.Year)
            .OrderByDescending(m => m.StartDate)
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<MemoryDto>>.Ok(memories.Select(m => m.ToDto()).ToList());
    }

    public async Task<ServiceResult<MemoryDto>> GetMemoryByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<MemoryDto>.Fail("Unauthorized", 401);
        }

        var memory = await QueryMemories(ctx.Value.FamilyId).FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        return memory is null
            ? ServiceResult<MemoryDto>.NotFound("Memory not found")
            : ServiceResult<MemoryDto>.Ok(memory.ToDto());
    }

    public async Task<ServiceResult<MemoryDto>> CreateMemoryAsync(
        CreateMemoryRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<MemoryDto>.Fail("Unauthorized", 401);
        }

        var memory = MapMemory(new Memory(), request, ctx.Value.FamilyId, ctx.Value.UserId);
        _db.Memories.Add(memory);
        await ApplyMemoryRelationsAsync(memory, request, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await LoadMemoryGraphAsync(memory.Id, cancellationToken);
        var loaded = await QueryMemories(ctx.Value.FamilyId).FirstAsync(m => m.Id == memory.Id, cancellationToken);
        return ServiceResult<MemoryDto>.Ok(loaded.ToDto());
    }

    public async Task<ServiceResult<MemoryDto>> UpdateMemoryAsync(
        Guid id,
        UpdateMemoryRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<MemoryDto>.Fail("Unauthorized", 401);
        }

        var memory = await _db.Memories
            .Include(m => m.MemoryPhotos)
            .Include(m => m.MemoryVideos)
            .Include(m => m.MemoryMembers)
            .FirstOrDefaultAsync(m => m.Id == id && m.FamilyId == ctx.Value.FamilyId, cancellationToken);

        if (memory is null)
        {
            return ServiceResult<MemoryDto>.NotFound("Memory not found");
        }

        MapMemory(memory, request, ctx.Value.FamilyId, ctx.Value.UserId);
        _db.MemoryPhotos.RemoveRange(memory.MemoryPhotos);
        _db.MemoryVideos.RemoveRange(memory.MemoryVideos);
        _db.MemoryMembers.RemoveRange(memory.MemoryMembers);
        await ApplyMemoryRelationsAsync(memory, request, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await LoadMemoryGraphAsync(memory.Id, cancellationToken);
        var loaded = await QueryMemories(ctx.Value.FamilyId).FirstAsync(m => m.Id == memory.Id, cancellationToken);
        return ServiceResult<MemoryDto>.Ok(loaded.ToDto());
    }

    public async Task<ServiceResult> DeleteMemoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult.Fail("Unauthorized", 401);
        }

        var memory = await _db.Memories.FirstOrDefaultAsync(m => m.Id == id && m.FamilyId == ctx.Value.FamilyId, cancellationToken);
        if (memory is null)
        {
            return ServiceResult.NotFound("Memory not found");
        }

        _db.Memories.Remove(memory);
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<IReadOnlyList<MemoryDto>>> FilterMemoriesAsync(
        MemoryFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<IReadOnlyList<MemoryDto>>.Fail("Unauthorized", 401);
        }

        var query = QueryMemories(ctx.Value.FamilyId);

        if (filter.Year.HasValue)
        {
            query = query.Where(m => m.StartDate.HasValue && m.StartDate.Value.Year == filter.Year.Value);
        }

        if (filter.MemberId.HasValue)
        {
            query = query.Where(m => m.MemoryMembers.Any(mm => mm.FamilyMemberId == filter.MemberId.Value));
        }

        if (filter.Generation.HasValue)
        {
            query = query.Where(m => m.Generation == filter.Generation.Value);
        }

        if (filter.EventType.HasValue)
        {
            query = query.Where(m => m.EventType == filter.EventType.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Location))
        {
            query = query.Where(m => m.Location != null && m.Location.Contains(filter.Location));
        }

        if (filter.Type.HasValue)
        {
            query = query.Where(m => m.Type == filter.Type.Value);
        }

        var memories = await query.OrderByDescending(m => m.CreatedAt).ToListAsync(cancellationToken);
        return ServiceResult<IReadOnlyList<MemoryDto>>.Ok(memories.Select(m => m.ToDto()).ToList());
    }

    private IQueryable<Memory> QueryMemories(Guid familyId) =>
        _db.Memories.AsNoTracking()
            .Where(m => m.FamilyId == familyId)
            .Include(m => m.MemoryPhotos)
            .Include(m => m.MemoryVideos)
            .Include(m => m.MemoryMembers);

    private static Memory MapMemory(Memory memory, CreateMemoryRequestDto request, Guid familyId, Guid userId)
    {
        memory.FamilyId = familyId;
        memory.CreatedByUserId = userId;
        memory.Title = request.Title;
        memory.Description = request.Description;
        memory.Type = request.Type;
        memory.TextContent = request.TextContent;
        memory.StartDate = request.StartDate;
        memory.EndDate = request.EndDate;
        memory.Location = request.Location;
        memory.EventType = request.EventType;
        memory.Generation = request.Generation;
        memory.BackgroundMusicId = request.BackgroundMusicId;
        memory.Visibility = request.Visibility;
        memory.WeatherSummary = request.WeatherSummary;
        return memory;
    }

    private async Task ApplyMemoryRelationsAsync(Memory memory, CreateMemoryRequestDto request, CancellationToken cancellationToken)
    {
        var photoOrder = 0;
        foreach (var photoId in request.PhotoIds)
        {
            _db.MemoryPhotos.Add(new MemoryPhoto { MemoryId = memory.Id, PhotoId = photoId, SortOrder = photoOrder++ });
        }

        var videoOrder = 0;
        foreach (var videoId in request.VideoIds)
        {
            _db.MemoryVideos.Add(new MemoryVideo { MemoryId = memory.Id, PhotoId = videoId, SortOrder = videoOrder++ });
        }

        foreach (var memberId in request.MemberIds)
        {
            _db.MemoryMembers.Add(new MemoryMember { MemoryId = memory.Id, FamilyMemberId = memberId });
        }

        await Task.CompletedTask;
    }

    private async Task LoadMemoryGraphAsync(Guid memoryId, CancellationToken cancellationToken)
    {
        _ = await _db.Memories
            .Include(m => m.MemoryPhotos)
            .Include(m => m.MemoryVideos)
            .Include(m => m.MemoryMembers)
            .FirstOrDefaultAsync(m => m.Id == memoryId, cancellationToken);
    }
}
