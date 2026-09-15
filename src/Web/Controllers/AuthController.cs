using System.Security.Claims;
using FlowDesk.Application.Features.Auth.Commands.Login;
using FlowDesk.Application.Features.Auth.Queries.GetCurrentUser;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowDesk.Web.Controllers;

public class AuthController : Controller
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated ?? false)
        {
            return RedirectToAction("Index", "Home");
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string email, string password, bool rememberMe = false, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            ModelState.AddModelError(string.Empty, "Email and Password are required.");
            return View();
        }

        try
        {
            var authResponse = await _mediator.Send(new LoginCommand(email, password));

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, authResponse.User.Id.ToString()),
                new Claim(ClaimTypes.Email, authResponse.User.Email),
                new Claim(ClaimTypes.Name, authResponse.User.FullName),
                new Claim(ClaimTypes.GivenName, authResponse.User.FirstName),
                new Claim(ClaimTypes.Surname, authResponse.User.LastName),
                new Claim("JobTitle", authResponse.User.JobTitle ?? string.Empty),
                new Claim("AccessToken", authResponse.AccessToken)
            };

            foreach (var role in authResponse.User.Roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            foreach (var perm in authResponse.User.Permissions)
            {
                claims.Add(new Claim("permission", perm));
            }

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc = authResponse.ExpiresAtUtc
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);

            TempData["Success"] = $"Welcome back, {authResponse.User.FullName}!";

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View();
        }
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["Success"] = "You have been logged out successfully.";
        return RedirectToAction("Login", "Auth");
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Profile()
    {
        try
        {
            var currentUser = await _mediator.Send(new GetCurrentUserQuery());
            return View(currentUser);
        }
        catch
        {
            return RedirectToAction("Index", "Home");
        }
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View("~/Views/Shared/AccessDenied.cshtml");
    }
}
