using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using schoolapp.Data;
using schoolapp.Models;

namespace schoolapp.Controllers;

[Authorize]
public sealed class PatientsController(
    ApplicationDbContext db,
    IConfiguration configuration) : HmsController(db, configuration)
{
    public async Task<IActionResult> Index(string? search)
    {
        var patients = Db.Patients.AsNoTracking().Where(patient => patient.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            patients = patients.Where(patient =>
                patient.FullName.Contains(term) || patient.PatientNumber.Contains(term) ||
                patient.Phone.Contains(term));
        }

        ViewBag.Search = search;
        return View(await patients.OrderByDescending(patient => patient.CreatedAtUtc)
            .Take(200).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var patient = await Db.Patients.AsNoTracking()
            .Include(item => item.Appointments).ThenInclude(item => item.Doctor)
            .SingleOrDefaultAsync(item => item.Id == id);
        return patient is null ? NotFound() : View(patient);
    }

    [Authorize(Roles = StaffRoles.Admin + "," + StaffRoles.Receptionist)]
    public IActionResult Create() => View(new Patient());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = StaffRoles.Admin + "," + StaffRoles.Receptionist)]
    public async Task<IActionResult> Create(Patient patient)
    {
        patient.PatientNumber = "Pending";
        patient.CreatedAtUtc = DateTime.UtcNow;
        if (patient.DateOfBirth == default ||
            patient.DateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            ModelState.AddModelError(nameof(patient.DateOfBirth), "Enter a valid date of birth that is not in the future.");
        }

        if (!ModelState.IsValid)
        {
            return View(patient);
        }

        await using var transaction = await Db.Database.BeginTransactionAsync();
        patient.PatientNumber = "PENDING-" + Guid.NewGuid().ToString("N");
        Db.Patients.Add(patient);
        await Db.SaveChangesAsync();
        patient.PatientNumber = $"PAT-{patient.Id:D6}";
        await Db.SaveChangesAsync();
        await AddAuditAsync("Patient created", "Patient", patient.Id);
        await transaction.CommitAsync();
        TempData["Success"] = $"Patient {patient.PatientNumber} was added.";
        return RedirectToAction(nameof(Details), new { id = patient.Id });
    }

    [Authorize(Roles = StaffRoles.Admin + "," + StaffRoles.Receptionist)]
    public async Task<IActionResult> Edit(int id)
    {
        var patient = await Db.Patients.SingleOrDefaultAsync(item => item.Id == id);
        return patient is null ? NotFound() : View(patient);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = StaffRoles.Admin + "," + StaffRoles.Receptionist)]
    public async Task<IActionResult> Edit(int id, Patient input)
    {
        if (id != input.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(input);
        }

        if (input.DateOfBirth == default ||
            input.DateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            ModelState.AddModelError(nameof(input.DateOfBirth), "Enter a valid date of birth that is not in the future.");
            return View(input);
        }

        var patient = await Db.Patients.SingleOrDefaultAsync(item => item.Id == id && item.IsActive);
        if (patient is null)
        {
            return NotFound();
        }

        patient.FullName = input.FullName.Trim();
        patient.DateOfBirth = input.DateOfBirth;
        patient.Gender = input.Gender;
        patient.Phone = input.Phone.Trim();
        patient.Email = input.Email.Trim();
        patient.Address = input.Address.Trim();
        await Db.SaveChangesAsync();
        await AddAuditAsync("Patient updated", "Patient", patient.Id);
        TempData["Success"] = "Patient details were updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = StaffRoles.Admin + "," + StaffRoles.Receptionist)]
    public async Task<IActionResult> Archive(int id)
    {
        var patient = await Db.Patients.SingleOrDefaultAsync(item => item.Id == id && item.IsActive);
        if (patient is null)
        {
            return NotFound();
        }

        patient.IsActive = false;
        await Db.SaveChangesAsync();
        await AddAuditAsync("Patient archived", "Patient", patient.Id);
        TempData["Success"] = "Patient was archived. Historical records were retained.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = StaffRoles.Admin + "," + StaffRoles.Receptionist)]
    public async Task<IActionResult> Delete(int id)
    {
        var patient = await Db.Patients.SingleOrDefaultAsync(item => item.Id == id);
        if (patient is null)
        {
            return NotFound();
        }

        var hasHistory =
            await Db.Appointments.AnyAsync(item => item.PatientId == id) ||
            await Db.MedicalRecords.AnyAsync(item => item.PatientId == id) ||
            await Db.Invoices.AnyAsync(item => item.PatientId == id);
        if (hasHistory)
        {
            TempData["Error"] = "This patient has appointment, clinical or billing history. Archive the profile instead to preserve those records.";
            return RedirectToAction(nameof(Details), new { id });
        }

        Db.Patients.Remove(patient);
        await Db.SaveChangesAsync();
        await AddAuditAsync("Patient deleted", "Patient", id);
        TempData["Success"] = "Patient record was permanently deleted.";
        return RedirectToAction(nameof(Index));
    }
}
