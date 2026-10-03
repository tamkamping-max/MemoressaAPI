using Memoressa.Application.Abstractions;
using Memoressa.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Application.Common;

public static class PhotoUploaderFamilyLabels
{
    public static async Task<IReadOnlyDictionary<(Guid FamilyId, Guid UserId), string>> LoadAsync(
        IMemoressaDbContext db,
        IReadOnlyList<Photo> photos,
        CancellationToken cancellationToken)
    {
        if (photos.Count == 0)
        {
            return new Dictionary<(Guid, Guid), string>();
        }

        var uploaderIds = photos.Select(p => p.UploadedByUserId).Distinct().ToList();
        var familyIds = photos.Select(p => p.FamilyId).Distinct().ToList();

        var users = await db.UserAccounts.AsNoTracking()
            .Where(u => uploaderIds.Contains(u.Id))
            .Select(u => new { u.Id, u.SelfFamilyMemberId })
            .ToListAsync(cancellationToken);

        var selfMemberIds = users
            .Where(u => u.SelfFamilyMemberId.HasValue)
            .Select(u => u.SelfFamilyMemberId!.Value)
            .Distinct()
            .ToList();

        var members = await db.FamilyMembers.AsNoTracking()
            .Where(m => familyIds.Contains(m.FamilyId)
                        && (m.LinkedUserId != null && uploaderIds.Contains(m.LinkedUserId.Value)
                            || selfMemberIds.Contains(m.Id)))
            .ToListAsync(cancellationToken);

        var usersById = users.ToDictionary(u => u.Id);
        var result = new Dictionary<(Guid FamilyId, Guid UserId), string>();

        foreach (var photo in photos)
        {
            var key = (photo.FamilyId, photo.UploadedByUserId);
            if (result.ContainsKey(key))
            {
                continue;
            }

            usersById.TryGetValue(photo.UploadedByUserId, out var userRow);
            FamilyMember? member = null;
            if (userRow?.SelfFamilyMemberId is Guid selfId)
            {
                member = members.FirstOrDefault(m => m.FamilyId == photo.FamilyId && m.Id == selfId);
            }

            member ??= members.FirstOrDefault(m =>
                m.FamilyId == photo.FamilyId && m.LinkedUserId == photo.UploadedByUserId);

            var label = PickMemberLabel(member);
            if (label is not null)
            {
                result[key] = label;
            }
        }

        return result;
    }

    private static string? PickMemberLabel(FamilyMember? member)
    {
        if (member is null)
        {
            return null;
        }

        var nickname = string.IsNullOrWhiteSpace(member.Nickname) ? null : member.Nickname.Trim();
        if (nickname is not null)
        {
            return nickname;
        }

        return string.IsNullOrWhiteSpace(member.Name) ? null : member.Name.Trim();
    }
}
