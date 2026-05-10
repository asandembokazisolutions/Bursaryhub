using System.Security.Claims;
using BursaryHub.Data;
using BursaryHub.Models;
using BursaryHub.Models.ViewModels;
using BursaryHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BursaryHub.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IBursaryScraper _scraper;
    private readonly IPasswordHasher _hasher;
    private readonly IEmailService _email;
    private readonly ILogger<AdminController> _logger;

    public AdminController(ApplicationDbContext db, IBursaryScraper scraper,
        IPasswordHasher hasher, IEmailService email, ILogger<AdminController> logger)
    {
        _db      = db;
        _scraper = scraper;
        _hasher  = hasher;
        _email   = email;
        _logger  = logger;
    }

    // ─── Dashboard ───────────────────────────────────────────────────────────

    public async Task<IActionResult> Index()
    {
        var vm = new AdminDashboardViewModel
        {
            TotalUsers           = await _db.Users.CountAsync(u => u.IsActive),
            TotalBursaries       = await _db.Bursaries.CountAsync(b => b.IsActive),
            TotalApplications    = await _db.BursaryApplications.CountAsync(),
            PendingApplications  = await _db.BursaryApplications.CountAsync(a => a.Status == "Pending"),
            ApplicationsByStatus = await _db.BursaryApplications
                .GroupBy(a => a.Status)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count),
            BursariesByProvider = await _db.Bursaries
                .Where(b => b.IsActive)
                .GroupBy(b => b.Provider)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count),
            TopBursaries = await _db.Bursaries
                .Where(b => b.IsActive)
                .Select(b => new TopBursaryItem { Name = b.Name, ApplicationCount = b.Applications.Count })
                .OrderByDescending(x => x.ApplicationCount)
                .Take(5)
                .ToListAsync(),
        };
        return View(vm);
    }

    // ─── Users ───────────────────────────────────────────────────────────────

    public async Task<IActionResult> Users(string? search, string? role, string? status, int page = 1)
    {
        const int pageSize = 20;
        var q = _db.Users.Include(u => u.Role).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(u => u.FirstName.Contains(search) || u.LastName.Contains(search) || u.Email.Contains(search));

        if (!string.IsNullOrWhiteSpace(role) && int.TryParse(role, out var rid))
            q = q.Where(u => u.RoleId == rid);

        if (status == "active")   q = q.Where(u => u.IsActive);
        if (status == "inactive") q = q.Where(u => !u.IsActive);

        var total = await q.CountAsync();
        var users = await q.OrderByDescending(u => u.CreatedDate)
                           .Skip((page - 1) * pageSize).Take(pageSize)
                           .ToListAsync();

        ViewBag.Total     = total;
        ViewBag.Page      = page;
        ViewBag.PageSize  = pageSize;
        ViewBag.Search    = search;
        ViewBag.Roles     = await _db.Roles.ToListAsync();

        return View(users);
    }

    public async Task<IActionResult> UserDetails(int id)
    {
        var user = await _db.Users
            .Include(u => u.Role)
            .Include(u => u.Applications).ThenInclude(a => a.Bursary)
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (user == null) return NotFound();
        return View(user);
    }

    [HttpGet]
    public async Task<IActionResult> EditUser(int id)
    {
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == id);
        if (user == null) return NotFound();

        var vm = new EditUserViewModel
        {
            UserId          = user.UserId,
            FirstName       = user.FirstName,
            LastName        = user.LastName,
            Email           = user.Email,
            PhoneNumber     = user.PhoneNumber ?? string.Empty,
            RoleId          = user.RoleId,
            IsActive        = user.IsActive,
            AvailableRoles  = await _db.Roles.Where(r => r.IsActive).ToListAsync(),
        };
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditUser(EditUserViewModel vm)
    {
        vm.AvailableRoles = await _db.Roles.Where(r => r.IsActive).ToListAsync();
        if (!ModelState.IsValid) return View(vm);

        var user = await _db.Users.FindAsync(vm.UserId);
        if (user == null) return NotFound();

        // Prevent admin from demoting themselves
        int currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (user.UserId == currentUserId && vm.RoleId != 1)
        {
            ModelState.AddModelError(string.Empty, "You cannot change your own role.");
            return View(vm);
        }

        // Ensure email uniqueness
        if (await _db.Users.AnyAsync(u => u.Email == vm.Email.ToLower() && u.UserId != vm.UserId))
        {
            ModelState.AddModelError(nameof(vm.Email), "Email is already in use.");
            return View(vm);
        }

        user.FirstName   = vm.FirstName.Trim();
        user.LastName    = vm.LastName.Trim();
        user.Email       = vm.Email.ToLowerInvariant().Trim();
        user.PhoneNumber = vm.PhoneNumber.Trim();
        user.RoleId      = vm.RoleId;
        user.IsActive    = vm.IsActive;

        await _db.SaveChangesAsync();
        _logger.LogInformation("Admin {Admin} updated user {UserId}", currentUserId, vm.UserId);
        TempData["Success"] = "User updated successfully.";
        return RedirectToAction(nameof(Users));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();

        int currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (user.UserId == currentUserId)
        {
            TempData["Error"] = "You cannot delete your own account.";
            return RedirectToAction(nameof(Users));
        }

        user.IsActive = false;
        await _db.SaveChangesAsync();
        _logger.LogInformation("Admin {Admin} deactivated user {UserId}", currentUserId, id);
        TempData["Success"] = "User deactivated successfully.";
        return RedirectToAction(nameof(Users));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReactivateUser(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();
        user.IsActive = true;
        await _db.SaveChangesAsync();
        TempData["Success"] = "User reactivated.";
        return RedirectToAction(nameof(Users));
    }

    // ─── Applications ────────────────────────────────────────────────────────

    [Authorize(Roles = "Admin,Moderator")]
    public async Task<IActionResult> Applications(string? status, string? search, int page = 1)
    {
        const int pageSize = 20;
        var q = _db.BursaryApplications
            .Include(a => a.User)
            .Include(a => a.Bursary)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(a => a.Status == status);

        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(a => a.User.FirstName.Contains(search)
                           || a.User.LastName.Contains(search)
                           || a.Bursary.Name.Contains(search));

        var total = await q.CountAsync();
        var apps  = await q.OrderByDescending(a => a.ApplicationDate)
                           .Skip((page - 1) * pageSize).Take(pageSize)
                           .ToListAsync();

        ViewBag.Total    = total;
        ViewBag.Page     = page;
        ViewBag.PageSize = pageSize;
        ViewBag.Status   = status;
        ViewBag.Search   = search;

        return View(apps);
    }

    [HttpGet, Authorize(Roles = "Admin,Moderator")]
    public async Task<IActionResult> ReviewApplication(int id)
    {
        var app = await _db.BursaryApplications
            .Include(a => a.User)
            .Include(a => a.Bursary)
            .FirstOrDefaultAsync(a => a.ApplicationId == id);

        if (app == null) return NotFound();
        ViewBag.Application = app;
        return View(new ReviewApplicationViewModel { ApplicationId = id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Admin,Moderator")]
    public async Task<IActionResult> ReviewApplication(ReviewApplicationViewModel vm)
    {
        var app = await _db.BursaryApplications
            .Include(a => a.User)
            .Include(a => a.Bursary)
            .FirstOrDefaultAsync(a => a.ApplicationId == vm.ApplicationId);

        if (app == null) return NotFound();

        if (!ModelState.IsValid)
        {
            ViewBag.Application = app;
            return View(vm);
        }

        if (app.Status != "Pending")
        {
            TempData["Error"] = "This application has already been reviewed.";
            return RedirectToAction(nameof(Applications));
        }

        int reviewerId   = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        app.Status           = vm.Decision; // "Approved" or "Rejected"
        app.ReviewedDate     = DateTime.UtcNow;
        app.ReviewedByUserId = reviewerId;
        app.ReviewNotes      = vm.ReviewNotes;
        await _db.SaveChangesAsync();

        await _email.SendApplicationDecisionAsync(
            app.User.Email, app.User.FirstName, app.Bursary.Name, vm.Decision, vm.ReviewNotes);

        _logger.LogInformation("Reviewer {R} {Decision} application {AppId}", reviewerId, vm.Decision, vm.ApplicationId);
        TempData["Success"] = $"Application {vm.Decision} and student notified.";
        return RedirectToAction(nameof(Applications));
    }

    // ─── Scraper ─────────────────────────────────────────────────────────────

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RunScraper()
    {
        var (added, skipped, msg) = await _scraper.ScrapeAsync();
        TempData["Success"] = msg;
        _logger.LogInformation("Scraper run by Admin {AdminId}: {Added} added, {Skipped} skipped",
            User.FindFirstValue(ClaimTypes.NameIdentifier), added, skipped);
        return RedirectToAction(nameof(Index));
    }
}
