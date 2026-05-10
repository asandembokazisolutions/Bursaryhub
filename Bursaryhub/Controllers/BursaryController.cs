using System.Security.Claims;
using BursaryHub.Data;
using BursaryHub.Models;
using BursaryHub.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BursaryHub.Controllers;

public class BursaryController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<BursaryController> _logger;

    public BursaryController(ApplicationDbContext db, ILogger<BursaryController> logger)
    {
        _db     = db;
        _logger = logger;
    }

    // ─── Browse (public) ─────────────────────────────────────────────────────

    public async Task<IActionResult> Index(BursarySearchViewModel search)
    {
        const int pageSize = 10;
        var q = _db.Bursaries
            .Include(b => b.Applications)
            .Where(b => b.IsActive && b.Status == "Active")
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search.Search))
            q = q.Where(b => b.Name.Contains(search.Search) || b.Provider.Contains(search.Search));

        if (search.MinAmount.HasValue)  q = q.Where(b => b.Amount >= search.MinAmount.Value);
        if (search.MaxAmount.HasValue)  q = q.Where(b => b.Amount <= search.MaxAmount.Value);

        if (search.DeadlineFilter == "7days")
            q = q.Where(b => b.ApplicationDeadline <= DateTime.UtcNow.AddDays(7) && b.ApplicationDeadline > DateTime.UtcNow);
        else if (search.DeadlineFilter == "30days")
            q = q.Where(b => b.ApplicationDeadline <= DateTime.UtcNow.AddDays(30) && b.ApplicationDeadline > DateTime.UtcNow);
        else if (search.DeadlineFilter == "more30")
            q = q.Where(b => b.ApplicationDeadline > DateTime.UtcNow.AddDays(30));

        q = search.SortBy switch
        {
            "amount"   => q.OrderByDescending(b => b.Amount),
            "provider" => q.OrderBy(b => b.Provider),
            "date"     => q.OrderByDescending(b => b.CreatedDate),
            _          => q.OrderBy(b => b.ApplicationDeadline),
        };

        var total    = await q.CountAsync();
        var bursaries = await q.Skip((search.Page - 1) * pageSize).Take(pageSize).ToListAsync();

        // Which bursaries has the current user already applied for?
        HashSet<int> applied = new();
        if (User.Identity?.IsAuthenticated == true)
        {
            int uid = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            applied = (await _db.BursaryApplications
                .Where(a => a.UserId == uid)
                .Select(a => a.BursaryId)
                .ToListAsync()).ToHashSet();
        }

        ViewBag.Applied   = applied;
        ViewBag.Total     = total;
        ViewBag.PageSize  = pageSize;
        ViewBag.Search    = search;

        return View(bursaries);
    }

    // ─── Details (public) ────────────────────────────────────────────────────

    public async Task<IActionResult> Details(int id)
    {
        var bursary = await _db.Bursaries
            .Include(b => b.Applications)
            .Include(b => b.CreatedByUser)
            .FirstOrDefaultAsync(b => b.BursaryId == id && b.IsActive);

        if (bursary == null) return NotFound();

        bool alreadyApplied = false;
        if (User.Identity?.IsAuthenticated == true)
        {
            int uid = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            alreadyApplied = await _db.BursaryApplications
                .AnyAsync(a => a.UserId == uid && a.BursaryId == id);
        }
        ViewBag.AlreadyApplied = alreadyApplied;
        return View(bursary);
    }

    // ─── Apply ───────────────────────────────────────────────────────────────

    [HttpPost, ValidateAntiForgeryToken, Authorize]
    public async Task<IActionResult> Apply(ApplyBursaryViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Please check your input.";
            return RedirectToAction(nameof(Details), new { id = vm.BursaryId });
        }

        int uid = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var bursary = await _db.Bursaries.FindAsync(vm.BursaryId);
        if (bursary == null || !bursary.IsActive)
        {
            TempData["Error"] = "This bursary is no longer available.";
            return RedirectToAction(nameof(Index));
        }

        if (bursary.IsDeadlinePassed)
        {
            TempData["Error"] = "The application deadline has passed.";
            return RedirectToAction(nameof(Details), new { id = vm.BursaryId });
        }

        if (await _db.BursaryApplications.AnyAsync(a => a.UserId == uid && a.BursaryId == vm.BursaryId))
        {
            TempData["Error"] = "You have already applied for this bursary.";
            return RedirectToAction(nameof(Details), new { id = vm.BursaryId });
        }

        _db.BursaryApplications.Add(new BursaryApplication
        {
            UserId           = uid,
            BursaryId        = vm.BursaryId,
            ApplicationNotes = vm.ApplicationNotes,
            ApplicationDate  = DateTime.UtcNow,
            Status           = "Pending",
        });
        await _db.SaveChangesAsync();

        _logger.LogInformation("User {UserId} applied for bursary {BursaryId}", uid, vm.BursaryId);
        TempData["Success"] = "Application submitted! Admins will review soon.";
        return RedirectToAction("MyApplications", "User");
    }

    // ─── Admin CRUD ──────────────────────────────────────────────────────────

    [Authorize(Roles = "Admin,Moderator")]
    [HttpGet]
    public IActionResult Create() => View(new BursaryFormViewModel());

    [Authorize(Roles = "Admin,Moderator")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BursaryFormViewModel vm)
    {
        if (vm.AwardDate <= vm.ApplicationDeadline)
            ModelState.AddModelError(nameof(vm.AwardDate), "Award Date must be after Application Deadline.");

        if (vm.ApplicationDeadline < DateTime.Today)
            ModelState.AddModelError(nameof(vm.ApplicationDeadline), "Deadline cannot be in the past.");

        if (!ModelState.IsValid) return View(vm);

        int uid = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        _db.Bursaries.Add(new Bursary
        {
            Name                = vm.Name.Trim(),
            Description         = vm.Description.Trim(),
            Provider            = vm.Provider.Trim(),
            Amount              = vm.Amount,
            ApplicationDeadline = vm.ApplicationDeadline,
            AwardDate           = vm.AwardDate,
            EligibilityCriteria = vm.EligibilityCriteria?.Trim(),
            ApplicationUrl      = vm.ApplicationUrl?.Trim(),
            Status              = "Active",
            IsActive            = true,
            IsScraped           = false,
            CreatedByUserId     = uid,
            CreatedDate         = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = "Bursary added successfully.";
        return RedirectToAction(nameof(Manage));
    }

    [Authorize(Roles = "Admin,Moderator")]
    public async Task<IActionResult> Manage(string? search, int page = 1)
    {
        const int pageSize = 10;
        var q = _db.Bursaries
            .Include(b => b.Applications)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(b => b.Name.Contains(search) || b.Provider.Contains(search));

        var total     = await q.CountAsync();
        var bursaries = await q.OrderByDescending(b => b.CreatedDate)
                               .Skip((page - 1) * pageSize).Take(pageSize)
                               .ToListAsync();

        ViewBag.Total    = total;
        ViewBag.Page     = page;
        ViewBag.PageSize = pageSize;
        ViewBag.Search   = search;
        return View(bursaries);
    }

    [Authorize(Roles = "Admin,Moderator")]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var b = await _db.Bursaries.FindAsync(id);
        if (b == null) return NotFound();

        return View(new BursaryFormViewModel
        {
            BursaryId           = b.BursaryId,
            Name                = b.Name,
            Description         = b.Description,
            Provider            = b.Provider,
            Amount              = b.Amount,
            ApplicationDeadline = b.ApplicationDeadline,
            AwardDate           = b.AwardDate,
            EligibilityCriteria = b.EligibilityCriteria,
            ApplicationUrl      = b.ApplicationUrl,
        });
    }

    [Authorize(Roles = "Admin,Moderator")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(BursaryFormViewModel vm)
    {
        if (vm.AwardDate <= vm.ApplicationDeadline)
            ModelState.AddModelError(nameof(vm.AwardDate), "Award Date must be after Application Deadline.");

        if (!ModelState.IsValid) return View(vm);

        var b = await _db.Bursaries.FindAsync(vm.BursaryId);
        if (b == null) return NotFound();

        b.Name                = vm.Name.Trim();
        b.Description         = vm.Description.Trim();
        b.Provider            = vm.Provider.Trim();
        b.Amount              = vm.Amount;
        b.ApplicationDeadline = vm.ApplicationDeadline;
        b.AwardDate           = vm.AwardDate;
        b.EligibilityCriteria = vm.EligibilityCriteria?.Trim();
        b.ApplicationUrl      = vm.ApplicationUrl?.Trim();
        b.UpdatedDate         = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        TempData["Success"] = "Bursary updated successfully.";
        return RedirectToAction(nameof(Manage));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var b = await _db.Bursaries.FindAsync(id);
        if (b == null) return NotFound();
        b.IsActive   = false;
        b.Status     = "Closed";
        b.UpdatedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Bursary deactivated.";
        return RedirectToAction(nameof(Manage));
    }
}
