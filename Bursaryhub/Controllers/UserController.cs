using System.Security.Claims;
using BursaryHub.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BursaryHub.Controllers;

[Authorize]
public class UserController : Controller
{
    private readonly ApplicationDbContext _db;

    public UserController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Profile()
    {
        int uid  = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == uid);
        if (user == null) return NotFound();
        return View(user);
    }

    public async Task<IActionResult> MyApplications(int page = 1)
    {
        const int pageSize = 10;
        int uid = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var q    = _db.BursaryApplications
                       .Include(a => a.Bursary)
                       .Where(a => a.UserId == uid);
        var total = await q.CountAsync();
        var apps  = await q.OrderByDescending(a => a.ApplicationDate)
                           .Skip((page - 1) * pageSize).Take(pageSize)
                           .ToListAsync();

        ViewBag.Total    = total;
        ViewBag.Page     = page;
        ViewBag.PageSize = pageSize;
        return View(apps);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> WithdrawApplication(int id)
    {
        int uid = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var app = await _db.BursaryApplications
            .FirstOrDefaultAsync(a => a.ApplicationId == id && a.UserId == uid);

        if (app == null) return NotFound();

        if (app.Status != "Pending")
        {
            TempData["Error"] = "Only pending applications can be withdrawn.";
            return RedirectToAction(nameof(MyApplications));
        }

        app.Status = "Withdrawn";
        await _db.SaveChangesAsync();
        TempData["Success"] = "Application withdrawn.";
        return RedirectToAction(nameof(MyApplications));
    }
}
