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

        if (currentUser.FamilyId.HasValue)
        {
            return (currentUser.UserId.Value, currentUser.FamilyId.Value);
        }

        var membership = await db.FamilyMemberships.AsNoTracking()
            .FirstOrDefaultAsync(m => m.UserId == currentUser.UserId.Value, cancellationToken);

        return membership is null ? null : (currentUser.UserId.Value, membership.FamilyId);
    }
}
