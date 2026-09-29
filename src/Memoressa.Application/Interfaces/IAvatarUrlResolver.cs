using Memoressa.Application.DTOs;
using Memoressa.Domain.Entities;

namespace Memoressa.Application.Interfaces;

public interface IAvatarUrlResolver
{
    Task<string?> ResolveForResponseAsync(string? storedAvatarUrl, CancellationToken cancellationToken = default);

    Task<UserDto> ToUserDtoAsync(UserAccount user, CancellationToken cancellationToken = default);

    Task<FamilyMemberDto> ToFamilyMemberDtoAsync(FamilyMember member, CancellationToken cancellationToken = default);
}
