using System.Security.Claims;
using BursaryHub.Models.ViewModels;
using BursaryHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BursaryHub.Controllers;

public class AccountController : Controller
{
    private readonly IAuthService _auth;
    private readonly ILogger<AccountController> _logger;

    public AccountController(IAuthService auth, ILogger<AccountController> logger)
    {
        _auth   = auth;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Register() => User.Identity?.IsAuthenticated == true
        ? RedirectToAction("Index", "Home")
        : View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var (ok, msg) = await _auth.RegisterAsync(
            model.FirstName, model.LastName, model.Email,
            model.PhoneNumber, model.Password, HttpContext);

        if (ok)
        {
            TempData["Success"] = msg;
            return RedirectToAction(nameof(Login));
        }

        ModelState.AddModelError(string.Empty, msg);
        return View(model);
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid) return View(model);

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var (ok, msg) = await _auth.LoginAsync(model.Email, model.Password, model.RememberMe, HttpContext, ip);

        if (ok)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction("Index", "Home");
        }

        ModelState.AddModelError(string.Empty, msg);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize]
    public async Task<IActionResult> Logout()
    {
        await _auth.LogoutAsync(HttpContext);
        TempData["Success"] = "You have been logged out.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public async Task<IActionResult> VerifyEmail(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            TempData["Error"] = "Invalid verification link.";
            return RedirectToAction(nameof(Login));
        }

        var ok = await _auth.VerifyEmailAsync(token);
        TempData[ok ? "Success" : "Error"] = ok
            ? "Email verified! You can now log in."
            : "Verification link is invalid or has expired.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult ForgotPassword() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var (_, msg) = await _auth.ForgotPasswordAsync(model.Email, HttpContext); // ✅ fixed
        TempData["Success"] = msg;
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult ResetPassword(string token, string email)
        => View(new ResetPasswordViewModel { Token = token, Email = email });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var (ok, msg) = await _auth.ResetPasswordAsync(model.Token, model.Email, model.NewPassword);
        TempData[ok ? "Success" : "Error"] = msg;
        return ok ? RedirectToAction(nameof(Login)) : View(model);
    }

    [HttpGet, Authorize]
    public IActionResult ChangePassword() => View();

    [HttpPost, ValidateAntiForgeryToken, Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var (ok, msg) = await _auth.ChangePasswordAsync(userId, model.CurrentPassword, model.NewPassword);

        TempData[ok ? "Success" : "Error"] = msg;
        if (ok) return RedirectToAction("Profile", "User");

        ModelState.AddModelError(string.Empty, msg);
        return View(model);
    }

    public IActionResult AccessDenied() => View();
}
