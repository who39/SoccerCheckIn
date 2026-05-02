using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SoccerCheckin.Web.Data;
using SoccerCheckin.Web.Models;
using System.Security.Claims;

namespace SoccerCheckin.Web.Controllers;

[AllowAnonymous]
public class AccountController(AppDbContext dbContext) : Controller
{
    [HttpPost]
    public IActionResult MicrosoftLogin(string? returnUrl = null)
    {
        var redirectUrl = Url.Action("Index", "Programs", new { returnUrl });
        var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
        return Challenge(properties, OpenIdConnectDefaults.AuthenticationScheme);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    public async Task LogOut()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme);
    }

    [HttpGet("signin-microsoft")]
    public async Task<IActionResult> MicrosoftCallback(string? returnUrl = null)
    {
        try
        {
            // OpenID Connect middleware handles authentication automatically
            if (User?.Identity?.IsAuthenticated == true)
            {
                // Extract Microsoft claims
                var email = User.FindFirst(ClaimTypes.Email)?.Value ?? User.FindFirst("preferred_username")?.Value;
                var name = User.FindFirst(ClaimTypes.Name)?.Value;
                var nameIdentifier = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (!string.IsNullOrEmpty(email))
                {
                    // Store or update user session
                    var existingUser = await dbContext.UserSessions
                        .FirstOrDefaultAsync(u => u.MicrosoftEmail == email);

                    if (existingUser != null)
                    {
                        existingUser.LastLoginUtc = DateTime.UtcNow;
                        existingUser.MicrosoftId = nameIdentifier ?? existingUser.MicrosoftId;
                        existingUser.MicrosoftDisplayName = name ?? existingUser.MicrosoftDisplayName;
                        dbContext.UserSessions.Update(existingUser);
                    }
                    else
                    {
                        var newUser = new UserSession
                        {
                            MicrosoftId = nameIdentifier ?? string.Empty,
                            MicrosoftEmail = email,
                            MicrosoftDisplayName = name,
                            Role = UserRole.User,
                            CreatedAtUtc = DateTime.UtcNow,
                            LastLoginUtc = DateTime.UtcNow
                        };
                        dbContext.UserSessions.Add(newUser);
                    }

                    await dbContext.SaveChangesAsync();
                }

                var redirectPath = !string.IsNullOrEmpty(returnUrl) ? returnUrl : Url.Action("Index", "Programs") ?? "/";
                return Redirect(redirectPath);
            }
        }
        catch (Exception ex)
        {
            // Log error and redirect to login
            return RedirectToAction("Login");
        }

        return RedirectToAction("Login");
    }
}
