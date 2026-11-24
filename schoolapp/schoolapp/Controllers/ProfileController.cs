using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using schoolapp.Data;
using schoolapp.Models;

namespace schoolapp.Controllers;

[Authorize]
public sealed class ProfileController(
    ApplicationDbContext db,
    IConfiguration configuration,
    IPasswordHasher<StaffUser> passwordHasher) : HmsController(db, configuration)
{
    [HttpGet]
    public IActionResult ChangePassword() => View(new ChangePasswordInput());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordInput input)
    {
        if (!ModelState.IsValid)
        {
            return View(input);
        }

        var staff = await Db.StaffUsers.SingleOrDefaultAsync(user =>
            user.Id == CurrentStaffId && user.IsActive);
        if (staff is null)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
        }

        if (passwordHasher.VerifyHashedPassword(staff, staff.PasswordHash, input.CurrentPassword) ==
            PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(nameof(input.CurrentPassword), "Current password is incorrect.");
            return View(input);
        }

        if (input.CurrentPassword == input.NewPassword)
        {
            ModelState.AddModelError(nameof(input.NewPassword), "Choose a password different from the current password.");
            return View(input);
        }

        staff.PasswordHash = passwordHasher.HashPassword(staff, input.NewPassword);
        await Db.SaveChangesAsync();
        await AddAuditAsync("Staff password changed", "StaffUser", staff.Id);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["Success"] = "Password changed. Sign in with your new password.";
        return RedirectToAction("Login", "Account");
    }
}
