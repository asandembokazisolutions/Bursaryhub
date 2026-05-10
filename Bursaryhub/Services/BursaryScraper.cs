using BursaryHub.Data;
using BursaryHub.Models;
using HtmlAgilityPack;

namespace BursaryHub.Services;

public interface IBursaryScraper
{
    Task<(int Added, int Skipped, string Message)> ScrapeAsync();
}

public class BursaryScraper : IBursaryScraper
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<BursaryScraper> _logger;
    private readonly IHttpClientFactory _httpFactory;

    // Simple throttle: only allow re-scrape every 24 hours
    private static DateTime _lastScrape = DateTime.MinValue;

    public BursaryScraper(ApplicationDbContext db, ILogger<BursaryScraper> logger, IHttpClientFactory httpFactory)
    {
        _db          = db;
        _logger      = logger;
        _httpFactory = httpFactory;
    }

    public async Task<(int Added, int Skipped, string Message)> ScrapeAsync()
    {
        if ((DateTime.UtcNow - _lastScrape).TotalHours < 24)
            return (0, 0, $"Scraper was last run at {_lastScrape.ToLocalTime():g}. Please wait 24 hours between runs.");

        var client = _httpFactory.CreateClient("Scraper");
        int added = 0, skipped = 0;

        // Example scrape targets – in production these would point to real scholarship listing pages
        var targets = new List<ScrapeTarget>
        {
            new("https://www.fastweb.com/college-scholarships", "//div[contains(@class,'scholarship-card')]", ParseFastweb),
        };

        foreach (var target in targets)
        {
            try
            {
                var html = await client.GetStringAsync(target.Url);
                var doc  = new HtmlDocument();
                doc.LoadHtml(html);

                var nodes = doc.DocumentNode.SelectNodes(target.XPath);
                if (nodes == null) { _logger.LogWarning("No nodes found at {Url}", target.Url); continue; }

                foreach (var node in nodes.Take(20)) // cap at 20 per source
                {
                    try
                    {
                        var bursary = target.Parser(node);
                        if (bursary == null) continue;

                        // Dedup by name
                        if (_db.Bursaries.Any(b => b.Name == bursary.Name))
                        { skipped++; continue; }

                        bursary.IsScraped   = true;
                        bursary.CreatedDate = DateTime.UtcNow;
                        bursary.Status      = "Active";
                        bursary.IsActive    = true;

                        _db.Bursaries.Add(bursary);
                        added++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse bursary node from {Url}", target.Url);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to scrape {Url}", target.Url);
            }
        }

        await _db.SaveChangesAsync();
        _lastScrape = DateTime.UtcNow;

        return (added, skipped, $"Scrape complete. {added} new bursaries added, {skipped} duplicates skipped.");
    }

    // ── Parsers ──────────────────────────────────────────────────────────────

    private static Bursary? ParseFastweb(HtmlNode node)
    {
        var name    = node.SelectSingleNode(".//h3")?.InnerText.Trim();
        var amount  = node.SelectSingleNode(".//*[contains(@class,'amount')]")?.InnerText.Trim();
        var desc    = node.SelectSingleNode(".//*[contains(@class,'description')]")?.InnerText.Trim();
        var link    = node.SelectSingleNode(".//a[@href]")?.GetAttributeValue("href", null);

        if (string.IsNullOrWhiteSpace(name)) return null;
        name = name.Length > 200 ? name[..200] : name;

        decimal parsedAmount = 0;
        if (!string.IsNullOrWhiteSpace(amount))
        {
            var digits = new string(amount.Where(c => char.IsDigit(c) || c == '.').ToArray());
            decimal.TryParse(digits, out parsedAmount);
        }

        return new Bursary
        {
            Name                = name,
            Description         = (desc ?? "No description available.").Length > 2000
                                    ? (desc ?? "No description available.")[..2000]
                                    : (desc ?? "No description available."),
            Provider            = "Fastweb",
            Amount              = parsedAmount > 0 ? parsedAmount : 1000m,
            ApplicationDeadline = DateTime.UtcNow.AddDays(30),
            AwardDate           = DateTime.UtcNow.AddDays(60),
            ApplicationUrl      = link != null ? (link.StartsWith("http") ? link : $"https://www.fastweb.com{link}") : null,
        };
    }

    private record ScrapeTarget(string Url, string XPath, Func<HtmlNode, Bursary?> Parser);
}
