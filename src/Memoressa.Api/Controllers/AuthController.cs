using Memoressa.Api.Extensions;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Memoressa.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _authService.RegisterAsync(request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _authService.RefreshTokenAsync(request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _authService.LogoutAsync(request.RefreshToken, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("password-reset/request")]
    [AllowAnonymous]
    public async Task<IActionResult> RequestPasswordReset([FromBody] PasswordResetRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _authService.RequestPasswordResetAsync(request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("password-reset/confirm")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _authService.ResetPasswordAsync(request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("password-reset/code/request")]
    [AllowAnonymous]
    public async Task<IActionResult> RequestPasswordResetCode(
        [FromBody] PasswordResetEmailRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.RequestPasswordResetCodeAsync(request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("password-reset/code/verify")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyPasswordResetCode(
        [FromBody] PasswordResetCodeVerifyRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.VerifyPasswordResetCodeAsync(request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("password-reset/code/confirm")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmPasswordResetWithCode(
        [FromBody] PasswordResetCodeConfirmRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.ConfirmPasswordResetWithCodeAsync(request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("account/deletion/schedule")]
    [Authorize]
    public async Task<IActionResult> ScheduleAccountDeletion([FromBody] AccountDeletionRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _authService.ScheduleAccountDeletionAsync(request.Password, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("account/deletion/cancel")]
    [Authorize]
    public async Task<IActionResult> CancelAccountDeletion(CancellationToken cancellationToken)
    {
        var result = await _authService.CancelAccountDeletionAsync(cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
    {
        var result = await _authService.GetCurrentUserAsync(cancellationToken);
        return result.ToActionResult();
    }

    [HttpPatch("me")]
    [Authorize]
    public async Task<IActionResult> PatchCurrentUser(
        [FromBody] PatchMeRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.PatchCurrentUserAsync(request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("account/deletion/status")]
    [Authorize]
    public async Task<IActionResult> GetDeletionStatus(CancellationToken cancellationToken)
    {
        var result = await _authService.GetDeletionStatusAsync(cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("oauth/google")]
    [AllowAnonymous]
    public async Task<IActionResult> LoginWithGoogle([FromBody] OAuthLoginRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginWithGoogleAsync(request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("oauth/facebook")]
    [AllowAnonymous]
    public async Task<IActionResult> LoginWithFacebook([FromBody] OAuthLoginRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginWithFacebookAsync(request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("oauth/apple")]
    [AllowAnonymous]
    public async Task<IActionResult> LoginWithApple([FromBody] AppleOAuthRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginWithAppleAsync(request, cancellationToken);
        return result.ToActionResult();
    }
}
