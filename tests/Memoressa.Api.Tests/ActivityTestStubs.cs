using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Domain.Entities;

namespace Memoressa.Api.Tests;

internal sealed class StubAvatarUrlResolver : IAvatarUrlResolver
{
    public Task<string?> ResolveForResponseAsync(string? storedAvatarUrl, CancellationToken cancellationToken = default) =>
        Task.FromResult(storedAvatarUrl);

    public Task<UserDto> ToUserDtoAsync(UserAccount user, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<FamilyMemberDto> ToFamilyMemberDtoAsync(FamilyMember member, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}
