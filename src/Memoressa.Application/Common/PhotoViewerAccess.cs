using Memoressa.Application.Abstractions;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Application.Common;

/// <summary>
/// Server-side CanViewerSeePhoto(U, photo) for lists and single-photo checks (App 1003).
/// </summary>
public static class PhotoViewerAccess
{
    public static IQueryable<Photo> ApplyViewerFilter(
        IQueryable<Photo> query,
        Guid viewerUserId,
        IMemoressaDbContext db) =>
        query.Where(p =>
            p.UploadedByUserId == viewerUserId
            || ((p.ActivityParticipantsVisible
                 || (p.PrivacyScope != UploadPrivacyScope.OnlySelf
                     && db.ActivityAlbumPhotos.Any(link => link.PhotoId == p.Id)))
                && db.ActivityAlbumPhotos.Any(aap =>
                    aap.PhotoId == p.Id
                    && (
                        aap.ActivityAlbum.CreatorUserId == viewerUserId
                        || (aap.ActivityAlbum.PrivacyScope == UploadPrivacyScope.Family
                            && db.FamilyMemberships.Any(m =>
                                m.UserId == viewerUserId && m.FamilyId == aap.ActivityAlbum.FamilyId))
                        || db.ActivityAlbumFamilyMembers.Any(afm =>
                            afm.ActivityAlbumId == aap.ActivityAlbumId
                            && db.FamilyMembers.Any(fm =>
                                fm.Id == afm.FamilyMemberId && fm.LinkedUserId == viewerUserId))
                        || db.ActivityAlbumFriends.Any(aff =>
                            aff.ActivityAlbumId == aap.ActivityAlbumId
                            && aff.FriendId != null
                            && db.Friends.Any(f =>
                                f.Id == aff.FriendId && f.FriendUserId == viewerUserId)))))
            || (p.PrivacyScope == UploadPrivacyScope.Family
                && db.FamilyMemberships.Any(m => m.UserId == viewerUserId && m.FamilyId == p.FamilyId)
                && (!db.PhotoMembers.Any(pm => pm.PhotoId == p.Id)
                    || db.PhotoMembers.Any(pm =>
                        pm.PhotoId == p.Id
                        && db.FamilyMembers.Any(fm =>
                            fm.Id == pm.FamilyMemberId
                            && (fm.LinkedUserId == viewerUserId
                                || db.FamilyMemberships.Any(m =>
                                    m.UserId == viewerUserId
                                    && m.FamilyId == fm.FamilyId
                                    && db.UserAccounts.Any(u =>
                                        u.Id == viewerUserId && u.SelfFamilyMemberId == fm.Id)))))))
            || (p.PrivacyScope == UploadPrivacyScope.Friends
                && ((!db.PhotoFriends.Any(pf => pf.PhotoId == p.Id)
                     && db.Friends.Any(f =>
                         f.OwnerUserId == p.UploadedByUserId && f.FriendUserId == viewerUserId))
                    || db.PhotoFriends.Any(pf =>
                        pf.PhotoId == p.Id
                        && db.Friends.Any(f =>
                            f.Id == pf.FriendId
                            && f.OwnerUserId == p.UploadedByUserId
                            && f.FriendUserId == viewerUserId))))
            || (p.PrivacyScope == UploadPrivacyScope.FriendsAndFamily
                && ((db.FamilyMemberships.Any(m => m.UserId == viewerUserId && m.FamilyId == p.FamilyId)
                     && (!db.PhotoMembers.Any(pm => pm.PhotoId == p.Id)
                         || db.PhotoMembers.Any(pm =>
                             pm.PhotoId == p.Id
                             && db.FamilyMembers.Any(fm =>
                                 fm.Id == pm.FamilyMemberId
                                 && (fm.LinkedUserId == viewerUserId
                                     || db.FamilyMemberships.Any(m =>
                                         m.UserId == viewerUserId
                                         && m.FamilyId == fm.FamilyId
                                         && db.UserAccounts.Any(u =>
                                             u.Id == viewerUserId && u.SelfFamilyMemberId == fm.Id)))))))
                    || db.Friends.Any(f =>
                        f.OwnerUserId == p.UploadedByUserId && f.FriendUserId == viewerUserId)
                    || db.PhotoFriends.Any(pf =>
                        pf.PhotoId == p.Id
                        && db.Friends.Any(f =>
                            f.Id == pf.FriendId
                            && f.OwnerUserId == p.UploadedByUserId
                            && f.FriendUserId == viewerUserId))))
            || (p.PrivacyScope == UploadPrivacyScope.Custom
                && (db.PhotoMembers.Any(pm =>
                        pm.PhotoId == p.Id
                        && db.FamilyMembers.Any(fm =>
                            fm.Id == pm.FamilyMemberId
                            && (fm.LinkedUserId == viewerUserId
                                || db.FamilyMemberships.Any(m =>
                                    m.UserId == viewerUserId
                                    && m.FamilyId == fm.FamilyId
                                    && db.UserAccounts.Any(u =>
                                        u.Id == viewerUserId && u.SelfFamilyMemberId == fm.Id)))))
                    || db.PhotoFriends.Any(pf =>
                        pf.PhotoId == p.Id
                        && db.Friends.Any(f =>
                            f.Id == pf.FriendId
                            && f.OwnerUserId == p.UploadedByUserId
                            && f.FriendUserId == viewerUserId)))));

    public static Task<bool> CanViewAsync(
        IMemoressaDbContext db,
        Guid photoId,
        Guid viewerUserId,
        CancellationToken cancellationToken = default) =>
        ApplyViewerFilter(db.Photos.AsNoTracking().Where(p => p.Id == photoId), viewerUserId, db)
            .AnyAsync(cancellationToken);

    public static Task<bool> CanViewAsync(
        IMemoressaDbContext db,
        Photo photo,
        Guid viewerUserId,
        CancellationToken cancellationToken = default) =>
        CanViewAsync(db, photo.Id, viewerUserId, cancellationToken);

    public static bool IsUploader(Photo photo, Guid userId) =>
        photo.UploadedByUserId == userId;
}
