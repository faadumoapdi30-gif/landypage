using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using schoolapp.Data;
using schoolapp.Models;

namespace schoolapp.Controllers;

[Authorize]
public sealed class DoctorsController(
    ApplicationDbContext db,
    IConfiguration configuration) : HmsController(db, configuration)
{
    public async Task<IActionResult> Index(string? search)
    {
        var query = Db.Doctors.AsNoTracking().Include(doctor => doctor.StaffUser)
            .AsQueryable();
        if (User.IsInRole(StaffRoles.Doctor))
        {
            query = query.Where(doctor => doctor.StaffUserId == CurrentStaffId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(doctor => doctor.FullName.Contains(term) ||
                doctor.Specialty.Contains(term) || doctor.Email.Contains(term));
        }

        ViewBag.Search = search;
        return View(await query.OrderBy(doctor => doctor.FullName).ToListAsync());
    }

    [Authorize(Roles = StaffRoles.Admin)]
    public async Task<IActionResult> Create()
    {
        await PopulateDoctorStaffOptionsAsync();
        return View(new DoctorInput());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = StaffRoles.Admin)]
    public async Task<IActionResult> Create(DoctorInput input)
    {
        await PopulateDoctorStaffOptionsAsync(input.StaffUserId);
        if (!await IsValidDoctorStaffLinkAsync(input.StaffUserId))
        {
            ModelState.AddModelError(nameof(input.StaffUserId),
                "Choose an active staff account with the Doctor role that is not already linked.");
        }

        if (!ModelState.IsValid)
        {
            return View(input);
        }

        var doctor = new Doctor
        {
            StaffUserId = input.StaffUserId,
            FullName = input.FullName.Trim(),
            Specialty = input.Specialty.Trim(),
            Phone = input.Phone.Trim(),
            Email = input.Email.Trim(),
            LicenseReference = input.LicenseReference.Trim(),
            IsActive = input.IsActive
        };
        Db.Doctors.Add(doctor);
        await Db.SaveChangesAsync();
        await AddAuditAsync("Doctor created", "Doctor", doctor.Id);
        TempData["Success"] = "Doctor profile was created.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = StaffRoles.Admin)]
    public async Task<IActionResult> Edit(int id)
    {
        var doctor = await Db.Doctors.FindAsync(id);
        if (doctor is null)
        {
            return NotFound();
        }

        await PopulateDoctorStaffOptionsAsync(doctor.StaffUserId, id);
        return View(new DoctorInput
        {
            StaffUserId = doctor.StaffUserId,
            FullName = doctor.FullName,
            Specialty = doctor.Specialty,
            Phone = doctor.Phone,
            Email = doctor.Email,
            LicenseReference = doctor.LicenseReference,
            IsActive = doctor.IsActive
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = StaffRoles.Admin)]
    public async Task<IActionResult> Edit(int id, DoctorInput input)
    {
        await PopulateDoctorStaffOptionsAsync(input.StaffUserId, id);
        if (!await IsValidDoctorStaffLinkAsync(input.StaffUserId, id))
        {
            ModelState.AddModelError(nameof(input.StaffUserId),
                "Choose an active staff account with the Doctor role that is not already linked.");
        }

        if (!ModelState.IsValid)
        {
            return View(input);
        }

        var doctor = await Db.Doctors.FindAsync(id);
        if (doctor is null)
        {
            return NotFound();
        }

        doctor.StaffUserId = input.StaffUserId;
        doctor.FullName = input.FullName.Trim();
        doctor.Specialty = input.Specialty.Trim();
        doctor.Phone = input.Phone.Trim();
        doctor.Email = input.Email.Trim();
        doctor.LicenseReference = input.LicenseReference.Trim();
        doctor.IsActive = input.IsActive;
        await Db.SaveChangesAsync();
        await AddAuditAsync("Doctor updated", "Doctor", doctor.Id);
        TempData["Success"] = "Doctor profile was updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = StaffRoles.Admin)]
    public async Task<IActionResult> Delete(int id)
    {
        var doctor = await Db.Doctors.FindAsync(id);
        if (doctor is null)
        {
            return NotFound();
        }

        var hasHistory =
            await Db.Appointments.AnyAsync(item => item.DoctorId == id) ||
            await Db.MedicalRecords.AnyAsync(item => item.DoctorId == id);
        if (hasHistory)
        {
            TempData["Error"] = "This doctor has appointment or clinical history. Edit the profile and deactivate it to preserve the records.";
            return RedirectToAction(nameof(Index));
        }

        Db.Doctors.Remove(doctor);
        await Db.SaveChangesAsync();
        await AddAuditAsync("Doctor deleted", "Doctor", id);
        TempData["Success"] = "Doctor profile was permanently deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateDoctorStaffOptionsAsync(int? selectedId = null, int? currentDoctorId = null)
    {
        ViewBag.DoctorStaff = await Db.StaffUsers.AsNoTracking()
            .Where(user => user.Role == StaffRoles.Doctor && user.IsActive &&
                (!Db.Doctors.Any(doctor => doctor.StaffUserId == user.Id && doctor.Id != currentDoctorId) ||
                 user.Id == selectedId))
            .OrderBy(user => user.FullName)
            .Select(user => new SelectListItem
            {
                Value = user.Id.ToString(),
                Text = user.FullName + " · " + user.Email,
                Selected = user.Id == selectedId
            }).ToListAsync();
    }

    private async Task<bool> IsValidDoctorStaffLinkAsync(int? staffUserId, int? currentDoctorId = null)
    {
        if (!staffUserId.HasValue)
        {
            return true;
        }

        var isEligible = await Db.StaffUsers.AnyAsync(user =>
            user.Id == staffUserId.Value && user.IsActive && user.Role == StaffRoles.Doctor);
        var isAlreadyLinked = await Db.Doctors.AnyAsync(doctor =>
            doctor.StaffUserId == staffUserId.Value && doctor.Id != currentDoctorId);
        return isEligible && !isAlreadyLinked;
    }
}
