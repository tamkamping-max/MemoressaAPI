using System.Text.Json.Serialization;
using Memoressa.Domain.Enums;

namespace Memoressa.Application.DTOs;

public record FamilyMemberUserSummaryDto
{
    [JsonPropertyName("id")] public Guid Id { get; init; }
    [JsonPropertyName("nickname")] public string? Nickname { get; init; }
    [JsonPropertyName("email")] public string? Email { get; init; }
    [JsonPropertyName("avatarUrl")] public string? AvatarUrl { get; init; }
}

public record FamilyMemberDto
{
    [JsonPropertyName("id")] public Guid Id { get; init; }
    [JsonPropertyName("familyConnectionId")] public Guid FamilyConnectionId => Id;
    [JsonPropertyName("memberConnectionId")] public Guid MemberConnectionId => Id;
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("nickname")] public string? Nickname { get; init; }
    [JsonPropertyName("birthDate")] public DateTime? BirthDate { get; init; }
    [JsonPropertyName("generation")] public Generation Generation { get; init; }
    [JsonPropertyName("relationship")] public string? Relationship { get; init; }
    [JsonPropertyName("avatarUrl")] public string? AvatarUrl { get; init; }
    [JsonPropertyName("cityId")] public string? CityId { get; init; }
    [JsonPropertyName("faceRecognitionEnabled")] public bool FaceRecognitionEnabled { get; init; }
    [JsonPropertyName("photoIds")] public IReadOnlyList<Guid> PhotoIds { get; init; } = [];
    [JsonPropertyName("connectionStatus")] public string ConnectionStatus { get; init; } = "accepted";
    [JsonPropertyName("status")] public string Status => ConnectionStatus;
    [JsonPropertyName("assignedToTree")] public bool AssignedToTree { get; init; } = true;
    [JsonPropertyName("email")] public string? Email { get; init; }
    [JsonPropertyName("linkedUserId")] public Guid? LinkedUserId { get; init; }
    [JsonPropertyName("friendUserId")] public Guid? FriendUserId => LinkedUserId;
    [JsonPropertyName("inviteId")] public Guid? InviteId { get; init; }
    [JsonPropertyName("linkedUser")] public FamilyMemberUserSummaryDto? LinkedUser { get; init; }
}

public record FamilyMemberInviteDto
{
    [JsonPropertyName("id")] public Guid Id { get; init; }
    [JsonPropertyName("inviteId")] public Guid InviteId => Id;
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("nickname")] public string? Nickname { get; init; }
    [JsonPropertyName("email")] public string Email { get; init; } = string.Empty;
    [JsonPropertyName("avatarUrl")] public string? AvatarUrl { get; init; }
    [JsonPropertyName("status")] public string Status { get; init; } = string.Empty;
    [JsonPropertyName("connectionStatus")] public string ConnectionStatus => Status;
    [JsonPropertyName("createdAt")] public DateTime CreatedAt { get; init; }
    [JsonPropertyName("inviter")] public FamilyMemberUserSummaryDto? Inviter { get; init; }
    [JsonPropertyName("invitee")] public FamilyMemberUserSummaryDto? Invitee { get; init; }
}

public record CreateFamilyMemberInviteRequestDto
{
    [JsonPropertyName("email")] public string Email { get; init; } = string.Empty;
}

public record CreateFamilyMemberRequestDto
{
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("nickname")] public string? Nickname { get; init; }
    [JsonPropertyName("birthDate")] public DateTime? BirthDate { get; init; }
    [JsonPropertyName("generation")] public Generation Generation { get; init; } = Generation.Self;
    [JsonPropertyName("relationship")] public string? Relationship { get; init; }
    [JsonPropertyName("avatarUrl")] public string? AvatarUrl { get; init; }
    [JsonPropertyName("cityId")] public string? CityId { get; init; }
    [JsonPropertyName("faceRecognitionEnabled")] public bool FaceRecognitionEnabled { get; init; } = true;
}

public record UpdateFamilyMemberRequestDto : CreateFamilyMemberRequestDto
{
    [JsonPropertyName("assignedToTree")] public bool? AssignedToTree { get; init; }
}

public record FamilyMomentDto
{
    [JsonPropertyName("id")] public Guid Id { get; init; }
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("eventType")] public EventType EventType { get; init; }
    [JsonPropertyName("date")] public DateTime Date { get; init; }
    [JsonPropertyName("photoIds")] public IReadOnlyList<Guid> PhotoIds { get; init; } = [];
    [JsonPropertyName("memberIds")] public IReadOnlyList<Guid> MemberIds { get; init; } = [];
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("isAiDiscovered")] public bool IsAiDiscovered { get; init; }
}

public record CreateFamilyMomentRequestDto
{
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("eventType")] public EventType EventType { get; init; }
    [JsonPropertyName("date")] public DateTime Date { get; init; }
    [JsonPropertyName("photoIds")] public IReadOnlyList<Guid> PhotoIds { get; init; } = [];
    [JsonPropertyName("memberIds")] public IReadOnlyList<Guid> MemberIds { get; init; } = [];
    [JsonPropertyName("description")] public string? Description { get; init; }
}

public record UpdateFamilyMomentRequestDto : CreateFamilyMomentRequestDto;
