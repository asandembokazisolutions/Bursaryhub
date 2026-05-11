using BursaryHub.Data;
using BursaryHub.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ─── Logging ─────────────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/app-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// ─── Database ───────────────────────────────────────────────────────────────
var connStr = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=bursaryhub.db;";

builder.Services.AddDbContext<ApplicationDbContext>(opts =>
    opts.UseSqlite(connStr)); // Change to UseSqlServer("Server=...") in production

// ─── Authentication ─────────────────────────────────────────────────────────
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, opts =>
    {
        opts.LoginPath = "/Account/Login";
        opts.LogoutPath = "/Account/Logout";
        opts.AccessDeniedPath = "/Account/AccessDenied";
        opts.ExpireTimeSpan = TimeSpan.FromMinutes(
            int.Parse(builder.Configuration["Authentication:CookieExpirationMinutes"] ?? "30"));
        opts.Cookie.HttpOnly = true;
        opts.Cookie.SecurePolicy = CookieSecurePolicy.Always; // HTTPS only
        opts.Cookie.SameSite = SameSiteMode.Strict;
    });

// ─── Services ───────────────────────────────────────────────────────────────
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IBursaryScraper, BursaryScraper>();

builder.Services.AddHttpClient("Scraper")
    .ConfigureHttpClient(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(
            int.Parse(builder.Configuration["Scraper:Timeout"] ?? "30"));
        client.DefaultRequestHeaders.Add("User-Agent",
            "BursaryHub/1.0 (+https://bursaryhub.example.com)");
    });

// ─── MVC ────────────────────────────────────────────────────────────────────
builder.Services.AddControllersWithViews();

// ─── CORS (if needed for future API) ────────────────────────────────────────
builder.Services.AddCors(opts =>
{
    opts.AddPolicy("AllowSelf", p => p.WithOrigins("https://yourdomain.com")
        .AllowAnyMethod().AllowAnyHeader());
});

// ─── Security Headers ───────────────────────────────────────────────────────
builder.Services.AddHsts(opts =>
{
    opts.MaxAge = TimeSpan.FromDays(365);
    opts.IncludeSubDomains = true;
});

var app = builder.Build();

// ─── Middleware ─────────────────────────────────────────────────────────────

// HTTPS Redirect (automatic with HSTS)
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Security Headers
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    ctx.Response.Headers["Content-Security-Policy"] = "default-src 'self' https:; script-src 'self' https: 'unsafe-inline'; style-src 'self' https: 'unsafe-inline'; font-src 'self' https: data:;";
    await next();
});

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");

// ─── Seed Database ──────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    // Ensure the directory exists before migrating
    var connString = db.Database.GetConnectionString();
    var dataSource = connString?.Split('=').LastOrDefault()?.Trim();
    if (!string.IsNullOrEmpty(dataSource))
        Directory.CreateDirectory(Path.GetDirectoryName(dataSource)!);

    db.Database.Migrate();
    await DbSeeder.SeedAdminAsync(scope.ServiceProvider);
}
app.Run();
