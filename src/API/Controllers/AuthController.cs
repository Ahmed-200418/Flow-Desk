using Asp.Versioning;
using FlowDesk.Application.Features.Auth.Commands.ForgotPassword;
using FlowDesk.Application.Features.Auth.Commands.Login;
using FlowDesk.Application.Features.Auth.Commands.RefreshToken;
using FlowDesk.Application.Features.Auth.Commands.ResetPassword;
using FlowDesk.Application.Features.Auth.Commands.RevokeToken;
using FlowDesk.Application.Features.Auth.Queries.GetCurrentUser;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowDesk.API.Controllers;

[ApiVersion("1.0")]
public class AuthController : ApiControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginCommand command)
    {
        var result = await Sender.Send(command);
        return Ok(result);
    }

    [HttpPost("refresh-token")]
    [AllowAnonymous]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenCommand command)
    {
        var result = await Sender.Send(command);
        return Ok(result);
    }

    [HttpPost("revoke-token")]
    [Authorize]
    public async Task<IActionResult> RevokeToken([FromBody] RevokeTokenCommand command)
    {
        await Sender.Send(command);
        return NoContent();
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordCommand command)
    {
        await Sender.Send(command);
        return Ok(new { Message = "If the email is registered, a password reset link has been dispatched." });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordCommand command)
    {
        await Sender.Send(command);
        return Ok(new { Message = "Password has been successfully reset." });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser()
    {
        var result = await Sender.Send(new GetCurrentUserQuery());
        return Ok(result);
    }
}
