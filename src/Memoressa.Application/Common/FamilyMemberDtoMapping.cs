using Memoressa.Application.DTOs;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;

namespace Memoressa.Application.Common;

public static class FamilyMemberDtoMapping
{
    /// <summary>Unassigned pool members omit generation in API responses (DB may still store a placeholder).</summary>
    public static Generation? GenerationForResponse(FamilyMember member) =>
        member.AssignedToTree ? member.Generation : null;
}
