using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using schoolapp.Data;
using schoolapp.Models;

namespace schoolapp.Controllers;

[Authorize(Roles = StaffRoles.Admin)]
public sealed class StaffController(
    ApplicationDbContext db,
    IConfiguration configuration,
    IPasswordHasher<StaffUser> passwordHasher) : HmsController(db, configuration)
{
    public async Task<IActionResult> Index()
    {
        return View(await Db.StaffUsers.AsNoTracking()
            .OrderBy(user => user.FullName).ToListAsync());
    }

    public IActionResult Create() => View(new StaffCreateInput());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StaffCreateInput input)
    {
        var allowedRoles = new[]
        {
            StaffRoles.Admin, StaffRoles.Receptionist, StaffRoles.Doctor, StaffRoles.Billing
        };
        if (!allowedRoles.Contains(input.Role))
        {
            ModelState.AddModelError(nameof(input.Role), "Choose a valid staff role.");
        }

        if (!ModelState.IsValid)
        {
            return View(input);
        }

        var email = input.Email.Trim().ToLowerInvariant();
        if (await Db.StaffUsers.AnyAsync(user => user.Email == email))
        {
            ModelState.AddModelError(nameof(input.Email), "An account with this email already exists.");
            return View(input);
        }

        var staff = new StaffUser
        {
            FullName = input.FullName.Trim(),
            Email = email,
            Role = input.Role,
            IsActive = true
        };
        staff.PasswordHash = passwordHasher.HashPassword(staff, input.Password);
        Db.StaffUsers.Add(staff);
        await Db.SaveChangesAsync();
        await AddAuditAsync("Staff account created", "StaffUser", staff.Id);
        TempData["Success"] = "Staff account was created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id)
    {
        if (id == CurrentStaffId)
        {
            TempData["Error"] = "You cannot deactivate your own account.";
            return RedirectToAction(nameof(Index));
        }

        var staff = await Db.StaffUsers.SingleOrDefaultAsync(user => user.Id == id && user.IsActive);
        if (staff is null)
        {
            return NotFound();
        }

        staff.IsActive = false;
        var linkedDoctor = await Db.Doctors.SingleOrDefaultAsync(doctor => doctor.StaffUserId == staff.Id);
        if (linkedDoctor is not null)
        {
            linkedDoctor.IsActive = false;
        }

        await Db.SaveChangesAsync();
        await AddAuditAsync("Staff account deactivated", "StaffUser", staff.Id);
        TempData["Success"] = "Staff account was deactivated.";
        return RedirectToAction(nameof(Index));
    }
}
