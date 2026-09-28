using Memoressa.Api.Extensions;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Memoressa.Api.Controllers;

[ApiController]
[Route("api/v1/family-moments")]
[Authorize]
public class FamilyMomentsController : ControllerBase
{
    private readonly IFamilyMomentService _familyMomentService;

    public FamilyMomentsController(IFamilyMomentService familyMomentService)
    {
        _familyMomentService = familyMomentService;
    }

    [HttpGet]
    public async Task<IActionResult> GetMoments(CancellationToken cancellationToken)
    {
        var result = await _familyMomentService.GetMomentsAsync(cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost]
    public async Task<IActionResult> CreateMoment([FromBody] CreateFamilyMomentRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _familyMomentService.CreateMomentAsync(request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateMoment(Guid id, [FromBody] UpdateFamilyMomentRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _familyMomentService.UpdateMomentAsync(id, request, cancellationToken);
        return result.ToActionResult();
    }
}
