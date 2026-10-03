using Memoressa.Application.Abstractions;
using Memoressa.Application.Common;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Application.Services;

internal static class ServiceHelpers
{
    public static ServiceResult<T> RequireUser<T>(ICurrentUserService currentUser) =>
        currentUser.UserId.HasValue
            ? ServiceResult<T>.Fail("Unauthorized", 401)
            : ServiceResult<T>.Fail("Unauthorized", 401);

    public static async Task<(Guid UserId, Guid FamilyId)?> ResolveFamilyAsync(
        ICurrentUserService currentUser,
        IMemoressaDbContext db,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return null;
        }

        var familyId = await ResolveMembershipFamilyIdAsync(
            currentUser.UserId.Value,
            currentUser.FamilyId,
            db,
            cancellationToken);

        return familyId.HasValue
            ? (currentUser.UserId.Value, familyId.Value)
            : null;
    }

    /// <summary>
    /// JWT <c>family_id</c> when the user belongs to that family; otherwise owner membership, then earliest membership.
    /// Shared by family roster scope and invite accept mirror placement.
    /// </summary>
    public static async Task<Guid?> ResolveMembershipFamilyIdAsync(
        Guid userId,
        Guid? jwtFamilyId,
        IMemoressaDbContext db,
        CancellationToken cancellationToken)
    {
        if (jwtFamilyId.HasValue)
        {
            var jwtIsMember = await db.FamilyMemberships.AsNoTracking()
                .AnyAsync(
                    m => m.UserId == userId && m.FamilyId == jwtFamilyId.Value,
                    cancellationToken);
            if (jwtIsMember)
            {
                return jwtFamilyId.Value;
            }
        }

        var membership = await db.FamilyMemberships.AsNoTracking()
            .Where(m => m.UserId == userId)
            .OrderBy(m => m.Role == "owner" ? 0 : 1)
            .ThenBy(m => m.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return membership?.FamilyId;
    }
}
