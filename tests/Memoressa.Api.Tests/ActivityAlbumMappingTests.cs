using Memoressa.Application.Common;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;

namespace Memoressa.Api.Tests;

public class ActivityAlbumMappingTests
{
    [Fact]
    public void ToDto_IncludesParticipantUserIds_AndViewerFlags()
    {
        var creatorId = Guid.NewGuid();
        var friendUserId = Guid.NewGuid();
        var linkedMemberUserId = Guid.NewGuid();
        var strangerId = Guid.NewGuid();

        var activity = new ActivityAlbum
        {
            ExternalId = "act_map",
            Title = "Trip",
            Type = ActivityAlbumType.Travel,
            Status = ActivityAlbumStatus.InProgress,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            CreatorUserId = creatorId,
            FamilyMembers =
            [
                new ActivityAlbumFamilyMember
                {
                    FamilyMember = new FamilyMember { LinkedUserId = linkedMemberUserId }
                }
            ],
            Friends =
            [
                new ActivityAlbumFriend
                {
                    FriendReference = "f1",
                    Friend = new Friend { FriendUserId = friendUserId }
                }
            ]
        };

        var dto = ActivityAlbumMapping.ToDto(activity, friendUserId);

        Assert.Equal(creatorId, dto.ParticipantUserIds[0]);
        Assert.Equal(3, dto.ParticipantUserIds.Count);
        Assert.Contains(linkedMemberUserId, dto.ParticipantUserIds);
        Assert.Contains(friendUserId, dto.ParticipantUserIds);
        Assert.False(dto.ViewerIsCreator);
        Assert.True(dto.ViewerIsParticipant);

        var creatorDto = ActivityAlbumMapping.ToDto(
            activity,
            creatorId,
            new ActivityAlbumCreatorSummary("Alice", "https://avatar/a.png"));
        Assert.True(creatorDto.ViewerIsCreator);
        Assert.True(creatorDto.ViewerIsParticipant);
        Assert.Equal("Alice", creatorDto.CreatorDisplayName);
        Assert.Equal("https://avatar/a.png", creatorDto.CreatorAvatarUrl);

        var outsider = ActivityAlbumMapping.ToDto(activity, strangerId);
        Assert.False(outsider.ViewerIsParticipant);
    }
}
