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

    // GET /Account/Register
    [HttpGet]
    public IActionResult Register() => User.Identity?.IsAuthenticated == true
        ? RedirectToAction("Index", "Home")
        : View();

    // POST /Account/Register
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
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

    // GET /Account/Login
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    // POST /Account/Login
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

    // POST /Account/Logout
    [HttpPost, ValidateAntiForgeryToken, Authorize]
    public async Task<IActionResult> Logout()
    {
        await _auth.LogoutAsync(HttpContext);
        TempData["Success"] = "You have been logged out.";
        return RedirectToAction(nameof(Login));
    }

    // GET /Account/VerifyEmail?token=...
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

    // GET /Account/ForgotPassword
    [HttpGet]
    public IActionResult ForgotPassword() => View();

    // POST /Account/ForgotPassword
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var (_, msg) = await _auth.ForgotPasswordAsync(model.Email);
        TempData["Success"] = msg;
        return RedirectToAction(nameof(Login));
    }

    // GET /Account/ResetPassword?token=...&email=...
    [HttpGet]
    public IActionResult ResetPassword(string token, string email)
        => View(new ResetPasswordViewModel { Token = token, Email = email });

    // POST /Account/ResetPassword
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var (ok, msg) = await _auth.ResetPasswordAsync(model.Token, model.Email, model.NewPassword);
        TempData[ok ? "Success" : "Error"] = msg;
        return ok ? RedirectToAction(nameof(Login)) : View(model);
    }

    // GET /Account/ChangePassword
    [HttpGet, Authorize]
    public IActionResult ChangePassword() => View();

    // POST /Account/ChangePassword
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

    // GET /Account/AccessDenied
    public IActionResult AccessDenied() => View();
}
