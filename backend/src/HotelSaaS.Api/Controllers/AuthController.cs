using HotelSaaS.Api.Controllers;
using HotelSaaS.Application.Common;
using HotelSaaS.Application.Dtos.Auth;
using HotelSaaS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelSaaS.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService auth) : ApiControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request) =>
        Wrap(await auth.LoginAsync(request));

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(RefreshRequest request) =>
        Wrap(await auth.RefreshAsync(request.RefreshToken));

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me() =>
        Wrap(await auth.GetProfileAsync(long.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value)));

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        await auth.ChangePasswordAsync(
            long.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value), request);
        return Wrap(new { changed = true }, "Password updated. Please sign in again on other devices.");
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request) =>
        Wrap(await auth.ForgotPasswordAsync(request));

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        await auth.ResetPasswordAsync(request);
        return Wrap(new { reset = true }, "Password has been reset.");
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest? request)
    {
        await auth.LogoutAsync(request?.RefreshToken);
        return Wrap(new { loggedOut = true });
    }
}



