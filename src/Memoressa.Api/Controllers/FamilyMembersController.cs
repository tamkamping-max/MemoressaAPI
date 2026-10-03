using Memoressa.Api.Extensions;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Memoressa.Api.Controllers;

[ApiController]
[Route("api/v1/family-members")]
[Authorize]
public class FamilyMembersController : ControllerBase
{
    private readonly IFamilyService _familyService;

    public FamilyMembersController(IFamilyService familyService)
    {
        _familyService = familyService;
    }

    [HttpGet]
    public async Task<IActionResult> GetMembers(CancellationToken cancellationToken)
    {
        var result = await _familyService.GetMembersAsync(cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("invites")]
    public async Task<IActionResult> GetFamilyMemberInvites(CancellationToken cancellationToken)
    {
        var result = await _familyService.GetFamilyMemberInvitesAsync(cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("invites")]
    public async Task<IActionResult> CreateFamilyMemberInvite(
        [FromBody] CreateFamilyMemberInviteRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _familyService.CreateFamilyMemberInviteAsync(request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("invites/{id:guid}/accept")]
    public async Task<IActionResult> AcceptFamilyMemberInvite(Guid id, CancellationToken cancellationToken)
    {
        var result = await _familyService.AcceptFamilyMemberInviteAsync(id, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("invites/{id:guid}/reject")]
    public async Task<IActionResult> RejectFamilyMemberInvite(Guid id, CancellationToken cancellationToken)
    {
        var result = await _familyService.RejectFamilyMemberInviteAsync(id, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetMemberById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _familyService.GetMemberByIdAsync(id, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost]
    public async Task<IActionResult> AddMember([FromBody] CreateFamilyMemberRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _familyService.AddMemberAsync(request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateMember(Guid id, [FromBody] UpdateFamilyMemberRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _familyService.UpdateMemberAsync(id, request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteMember(Guid id, CancellationToken cancellationToken)
    {
        var result = await _familyService.DeleteMemberAsync(id, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("by-generation/{generation}")]
    public async Task<IActionResult> GetMembersByGeneration(Generation generation, CancellationToken cancellationToken)
    {
        var result = await _familyService.GetMembersByGenerationAsync(generation, cancellationToken);
        return result.ToActionResult();
    }
}
