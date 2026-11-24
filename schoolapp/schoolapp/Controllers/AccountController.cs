using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using schoolapp.Data;
using schoolapp.Models;

namespace schoolapp.Controllers;

[AllowAnonymous]
public sealed class AccountController(
    ApplicationDbContext db,
    IPasswordHasher<StaffUser> passwordHasher,
    IConfiguration configuration,
    ILogger<AccountController> logger) : Controller
{
    private bool IsDatabaseConfigured =>
        !string.IsNullOrWhiteSpace(configuration.GetConnectionString("Supabase"));

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!IsDatabaseConfigured)
        {
            return RedirectToAction(nameof(Setup));
        }

        try
        {
            if (!await db.Database.CanConnectAsync())
            {
                TempData["Error"] = "The hospital database could not be reached. Check the connection settings.";
                return RedirectToAction(nameof(Login));
            }

            if (!await db.StaffUsers.AnyAsync())
            {
                return RedirectToAction(nameof(Setup));
            }
        }
        catch (Npgsql.NpgsqlException exception)
        {
            logger.LogError(exception, "Could not read the HMS staff table.");
            TempData["Error"] = "The database schema is not ready. Apply the HMS migrations, then try again.";
        }

        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public async Task<IActionResult> Setup()
    {
        ViewBag.DatabaseConfigured = IsDatabaseConfigured;
        if (IsDatabaseConfigured)
        {
            try
            {
                if (!await db.Database.CanConnectAsync())
                {
                    ViewBag.SetupError = "The database could not be reached. Verify the connection settings and network access.";
                }
                else if (await db.StaffUsers.AnyAsync())
                {
                    return RedirectToAction(nameof(Login));
                }
            }
            catch (Npgsql.NpgsqlException exception)
            {
                logger.LogError(exception, "First-run setup could not read the HMS schema.");
                ViewBag.SetupError = "The database is reachable, but the HMS schema is not ready. Apply the database migrations first.";
            }
        }

        return View(new StaffSetupInput());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> Setup(StaffSetupInput input)
    {
        ViewBag.DatabaseConfigured = IsDatabaseConfigured;
        if (!IsDatabaseConfigured)
        {
            ModelState.AddModelError(string.Empty,
                "Configure ConnectionStrings:Supabase in .NET user-secrets or the deployment secret store first.");
            return View(input);
        }

        if (!ModelState.IsValid)
        {
            return View(input);
        }

        try
        {
            if (!await db.Database.CanConnectAsync())
            {
                ModelState.AddModelError(string.Empty,
                    "The database could not be reached. Check the connection settings and network access.");
                return View(input);
            }

            await using var transaction = await db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable);
            if (await db.StaffUsers.AnyAsync())
            {
                await transaction.RollbackAsync();
                return RedirectToAction(nameof(Login));
            }

            var admin = new StaffUser
            {
                FullName = input.FullName.Trim(),
                Email = input.Email.Trim().ToLowerInvariant(),
                Role = StaffRoles.Admin,
                IsActive = true
            };
            admin.PasswordHash = passwordHasher.HashPassword(admin, input.Password);
            db.StaffUsers.Add(admin);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();

            await SignInAsync(admin);
            return RedirectToAction("Index", "Dashboard");
        }
        catch (Npgsql.NpgsqlException exception)
        {
            logger.LogError(exception, "Database setup failed while creating the first HMS administrator.");
            ModelState.AddModelError(string.Empty,
                "The database is reachable but the HMS schema is missing or unavailable. Apply the database migrations and try again.");
            return View(input);
        }
        catch (DbUpdateException exception)
        {
            logger.LogError(exception, "Could not save the initial HMS administrator.");
            ModelState.AddModelError(string.Empty,
                "The administrator account could not be created. Check the database schema and ensure the email has not already been registered.");
            return View(input);
        }
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewBag.DatabaseConfigured = IsDatabaseConfigured;
        ViewBag.ReturnUrl = returnUrl;
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        return View(new LoginInput { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("account")]
    public async Task<IActionResult> Login(LoginInput input)
    {
        ViewBag.DatabaseConfigured = IsDatabaseConfigured;
        if (!IsDatabaseConfigured)
        {
            ModelState.AddModelError(string.Empty,
                "The hospital database is not configured. Set ConnectionStrings:Supabase before signing in.");
            return View(input);
        }

        if (!ModelState.IsValid)
        {
            return View(input);
        }

        try
        {
            var email = input.Email.Trim().ToLowerInvariant();
            var user = await db.StaffUsers.SingleOrDefaultAsync(account => account.Email == email);
            if (user is null || !user.IsActive)
            {
                ModelState.AddModelError(string.Empty, "Email or password is incorrect.");
                return View(input);
            }

            var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, input.Password);
            if (verification == PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError(string.Empty, "Email or password is incorrect.");
                return View(input);
            }

            if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            {
                user.PasswordHash = passwordHasher.HashPassword(user, input.Password);
                await db.SaveChangesAsync();
            }

            await SignInAsync(user);
            if (!string.IsNullOrWhiteSpace(input.ReturnUrl) && Url.IsLocalUrl(input.ReturnUrl))
            {
                return LocalRedirect(input.ReturnUrl);
            }

            return RedirectToAction("Index", "Dashboard");
        }
        catch (Npgsql.NpgsqlException exception)
        {
            logger.LogError(exception, "Database sign-in failed.");
            ModelState.AddModelError(string.Empty,
                "The database schema is unavailable. Apply the HMS migrations and try again.");
            return View(input);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    private async Task SignInAsync(StaffUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = false, AllowRefresh = true });
    }
}
