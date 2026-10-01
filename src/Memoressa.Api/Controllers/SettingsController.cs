using Memoressa.Api.Extensions;
using Memoressa.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Memoressa.Api.Controllers;

[ApiController]
[Route("api/v1/settings")]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly ISettingsService _settingsService;

    public SettingsController(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    [HttpGet("locale")]
    public async Task<IActionResult> GetLocale(CancellationToken cancellationToken)
    {
        var result = await _settingsService.GetLocaleAsync(cancellationToken);
        return result.ToActionResult();
    }

    [HttpPut("locale")]
    public async Task<IActionResult> SetLocale([FromBody] LocaleRequest request, CancellationToken cancellationToken)
    {
        var result = await _settingsService.SetLocaleAsync(request.Locale, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("onboarding")]
    public async Task<IActionResult> IsOnboardingComplete(CancellationToken cancellationToken)
    {
        var result = await _settingsService.IsOnboardingCompleteAsync(cancellationToken);
        return result.ToActionResult();
    }

    [HttpPut("onboarding")]
    public async Task<IActionResult> SetOnboardingComplete([FromBody] OnboardingRequest request, CancellationToken cancellationToken)
    {
        var result = await _settingsService.SetOnboardingCompleteAsync(request.Complete, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("ai")]
    public async Task<IActionResult> GetAiSettings(CancellationToken cancellationToken)
    {
        var result = await _settingsService.GetAiSettingsAsync(cancellationToken);
        return result.ToActionResult();
    }

    [HttpPut("ai")]
    public async Task<IActionResult> UpdateAiSettings([FromBody] Dictionary<string, bool> settings, CancellationToken cancellationToken)
    {
        var result = await _settingsService.UpdateAiSettingsAsync(settings, cancellationToken);
        return result.ToActionResult();
    }

    public record LocaleRequest(string Locale);

    public record OnboardingRequest(bool Complete);
}
