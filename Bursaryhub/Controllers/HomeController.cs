using BursaryHub.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BursaryHub.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _db;

    public HomeController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var featured = await _db.Bursaries
            .Where(b => b.IsActive && b.Status == "Active" && b.ApplicationDeadline > DateTime.UtcNow)
            .OrderBy(b => b.ApplicationDeadline)
            .Take(6)
            .ToListAsync();
        return View(featured);
    }

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View();
}
