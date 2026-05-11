using BursaryHub.Data;
using BursaryHub.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// ─── Logging ─────────────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/app-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// ─── Database ───────────────────────────────────────────────────────────────
string connStr;

if (builder.Environment.IsDevelopment())
{
    // ✅ Local: SQLite — no secrets needed
    connStr = "Data Source=bursaryhub.db;";
    builder.Services.AddDbContext<ApplicationDbContext>(opts =>
        opts.UseSqlite(connStr));
}
else
{
    var dbHost = Environment.GetEnvironmentVariable("DB_HOST")
        ?? throw new InvalidOperationException("DB_HOST is not set.");
    var dbPort = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
    var dbName = Environment.GetEnvironmentVariable("DB_NAME")
        ?? throw new InvalidOperationException("DB_NAME is not set.");
    var dbUser = Environment.GetEnvironmentVariable("DB_USER")
        ?? throw new InvalidOperationException("DB_USER is not set.");
    var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD")
        ?? throw new InvalidOperationException("DB_PASSWORD is not set.");

    // ✅ Use NpgsqlDataSourceBuilder to disable multiplexing properly
    var dataSourceBuilder = new NpgsqlDataSourceBuilder(
        $"Host={dbHost};Port={dbPort};Database={dbName};Username={dbUser};Password={dbPassword};SSL Mode=Require;Trust Server Certificate=true;");
    dataSourceBuilder.UseLoggerFactory(LoggerFactory.Create(b => b.AddSerilog()));
    var dataSource = dataSourceBuilder.Build();

    builder.Services.AddDbContext<ApplicationDbContext>(opts =>
        opts.UseNpgsql(dataSource));
}

// ─── Authentication ─────────────────────────────────────────────────────────
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, opts =>
    {
        opts.LoginPath = "/Account/Login";
        opts.LogoutPath = "/Account/Logout";
        opts.AccessDeniedPath = "/Account/AccessDenied";
        opts.ExpireTimeSpan = TimeSpan.FromMinutes(
            int.Parse(Environment.GetEnvironmentVariable("COOKIE_EXPIRY_MINUTES") ?? "30"));
        opts.Cookie.HttpOnly = true;
        opts.Cookie.SecurePolicy = CookieSecurePolicy.Always;
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
            int.Parse(Environment.GetEnvironmentVariable("SCRAPER_TIMEOUT") ?? "30"));
        client.DefaultRequestHeaders.Add("User-Agent",
            "BursaryHub/1.0 (+https://bursaryhub.example.com)");
    });

// ─── MVC ────────────────────────────────────────────────────────────────────
builder.Services.AddControllersWithViews();

// ─── CORS ───────────────────────────────────────────────────────────────────
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
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.Migrate();
        await DbSeeder.SeedAdminAsync(scope.ServiceProvider);
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "Database migration or seeding failed.");
        throw;
    }
}

app.Run();
