using Memoressa.Application.Abstractions;
using Memoressa.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Infrastructure.Data;

public static class EfTextSearch
{
    public static string ToLikePattern(string query)
    {
        var trimmed = query.Trim();
        var escaped = trimmed
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
        return $"%{escaped}%";
    }

    public static bool IsPostgreSqlProvider(IMemoressaDbContext db) =>
        db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;

    public static IQueryable<Memory> WhereMemoryTextMatches(
        IQueryable<Memory> query,
        string likePattern,
        bool usePostgreSql)
    {
        if (usePostgreSql)
        {
            return query.Where(m =>
                EF.Functions.ILike(m.Title, likePattern)
                || EF.Functions.ILike(m.Description ?? string.Empty, likePattern)
                || EF.Functions.ILike(m.Location ?? string.Empty, likePattern));
        }

        return query.Where(m =>
            EF.Functions.Like(m.Title, likePattern)
            || (m.Description != null && EF.Functions.Like(m.Description, likePattern))
            || (m.Location != null && EF.Functions.Like(m.Location, likePattern)));
    }

    public static IQueryable<Photo> WherePhotoTagMatches(
        IQueryable<Photo> query,
        string likePattern,
        bool usePostgreSql) =>
        WherePhotoAgentFieldMatches(query, likePattern, usePostgreSql);

    /// <summary>Photo fields used by AI Agent search (description, location, user/AI tags).</summary>
    public static IQueryable<Photo> WherePhotoAgentFieldMatches(
        IQueryable<Photo> query,
        string likePattern,
        bool usePostgreSql)
    {
        if (usePostgreSql)
        {
            return query.Where(p =>
                EF.Functions.ILike(p.Description ?? string.Empty, likePattern)
                || EF.Functions.ILike(p.Location ?? string.Empty, likePattern)
                || p.AiTags.Any(t => EF.Functions.ILike(t.Tag, likePattern))
                || p.UserTags.Any(t => EF.Functions.ILike(t.Tag, likePattern)));
        }

        return query.Where(p =>
            (p.Description != null && EF.Functions.Like(p.Description, likePattern))
            || (p.Location != null && EF.Functions.Like(p.Location, likePattern))
            || p.AiTags.Any(t => EF.Functions.Like(t.Tag, likePattern))
            || p.UserTags.Any(t => EF.Functions.Like(t.Tag, likePattern)));
    }

    public static IQueryable<Photo> WherePhotoTakenAtYearIn(
        IQueryable<Photo> query,
        IReadOnlyList<int> years)
    {
        if (years.Count == 0)
        {
            return query.Where(_ => false);
        }

        return query.Where(p => p.TakenAt.HasValue && years.Contains(p.TakenAt.Value.Year));
    }

    /// <summary>Photo album fields for AI Agent search (description, user tags, comments).</summary>
    public static IQueryable<PhotoAlbum> WherePhotoAlbumAgentFieldMatches(
        IQueryable<PhotoAlbum> query,
        string likePattern,
        bool usePostgreSql)
    {
        if (usePostgreSql)
        {
            return query.Where(a =>
                EF.Functions.ILike(a.Description ?? string.Empty, likePattern)
                || a.UserTags.Any(t => EF.Functions.ILike(t.Tag, likePattern))
                || a.Comments.Any(c => EF.Functions.ILike(c.Message, likePattern)));
        }

        return query.Where(a =>
            (a.Description != null && EF.Functions.Like(a.Description, likePattern))
            || a.UserTags.Any(t => EF.Functions.Like(t.Tag, likePattern))
            || a.Comments.Any(c => EF.Functions.Like(c.Message, likePattern)));
    }

    public static IQueryable<Memory> WhereMemoryTextMatchesAny(
        IQueryable<Memory> query,
        IReadOnlyList<string> likePatterns,
        bool usePostgreSql)
    {
        if (likePatterns.Count == 0)
        {
            return query.Where(_ => false);
        }

        if (likePatterns.Count == 1)
        {
            return WhereMemoryTextMatches(query, likePatterns[0], usePostgreSql);
        }

        if (usePostgreSql)
        {
            return query.Where(m =>
                likePatterns.Any(p => EF.Functions.ILike(m.Title, p))
                || likePatterns.Any(p => EF.Functions.ILike(m.Description ?? string.Empty, p))
                || likePatterns.Any(p => EF.Functions.ILike(m.Location ?? string.Empty, p)));
        }

        return query.Where(m =>
            likePatterns.Any(p => EF.Functions.Like(m.Title, p))
            || likePatterns.Any(p => m.Description != null && EF.Functions.Like(m.Description, p))
            || likePatterns.Any(p => m.Location != null && EF.Functions.Like(m.Location, p)));
    }

    public static IQueryable<Photo> WherePhotoAgentFieldMatchesAny(
        IQueryable<Photo> query,
        IReadOnlyList<string> likePatterns,
        bool usePostgreSql)
    {
        if (likePatterns.Count == 0)
        {
            return query.Where(_ => false);
        }

        if (likePatterns.Count == 1)
        {
            return WherePhotoAgentFieldMatches(query, likePatterns[0], usePostgreSql);
        }

        if (usePostgreSql)
        {
            return query.Where(p =>
                likePatterns.Any(pat => EF.Functions.ILike(p.Description ?? string.Empty, pat))
                || likePatterns.Any(pat => EF.Functions.ILike(p.Location ?? string.Empty, pat))
                || p.AiTags.Any(t => likePatterns.Any(pat => EF.Functions.ILike(t.Tag, pat)))
                || p.UserTags.Any(t => likePatterns.Any(pat => EF.Functions.ILike(t.Tag, pat))));
        }

        return query.Where(p =>
            likePatterns.Any(pat => p.Description != null && EF.Functions.Like(p.Description, pat))
            || likePatterns.Any(pat => p.Location != null && EF.Functions.Like(p.Location, pat))
            || p.AiTags.Any(t => likePatterns.Any(pat => EF.Functions.Like(t.Tag, pat)))
            || p.UserTags.Any(t => likePatterns.Any(pat => EF.Functions.Like(t.Tag, pat))));
    }

    public static IQueryable<PhotoAlbum> WherePhotoAlbumAgentFieldMatchesAny(
        IQueryable<PhotoAlbum> query,
        IReadOnlyList<string> likePatterns,
        bool usePostgreSql)
    {
        if (likePatterns.Count == 0)
        {
            return query.Where(_ => false);
        }

        if (likePatterns.Count == 1)
        {
            return WherePhotoAlbumAgentFieldMatches(query, likePatterns[0], usePostgreSql);
        }

        if (usePostgreSql)
        {
            return query.Where(a =>
                likePatterns.Any(p => EF.Functions.ILike(a.Description ?? string.Empty, p))
                || a.UserTags.Any(t => likePatterns.Any(p => EF.Functions.ILike(t.Tag, p)))
                || a.Comments.Any(c => likePatterns.Any(p => EF.Functions.ILike(c.Message, p))));
        }

        return query.Where(a =>
            likePatterns.Any(p => a.Description != null && EF.Functions.Like(a.Description, p))
            || a.UserTags.Any(t => likePatterns.Any(p => EF.Functions.Like(t.Tag, p)))
            || a.Comments.Any(c => likePatterns.Any(p => EF.Functions.Like(c.Message, p))));
    }
}
