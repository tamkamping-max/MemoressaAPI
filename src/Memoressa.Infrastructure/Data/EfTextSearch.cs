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
        bool usePostgreSql)
    {
        if (usePostgreSql)
        {
            return query.Where(p => p.AiTags.Any(t => EF.Functions.ILike(t.Tag, likePattern)));
        }

        return query.Where(p => p.AiTags.Any(t => EF.Functions.Like(t.Tag, likePattern)));
    }
}
