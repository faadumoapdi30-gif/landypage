using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using schoolapp.Data;
using schoolapp.Models;

namespace schoolapp.Controllers;

[Authorize(Roles = StaffRoles.Admin + "," + StaffRoles.Doctor)]
public sealed class MedicalRecordsController(
    ApplicationDbContext db,
    IConfiguration configuration) : HmsController(db, configuration)
{
    public async Task<IActionResult> Index()
    {
        var records = Db.MedicalRecords.AsNoTracking()
            .Include(record => record.Patient)
            .Include(record => record.Doctor)
            .AsQueryable();
        if (User.IsInRole(StaffRoles.Doctor))
        {
            records = records.Where(record => record.Doctor.StaffUserId == CurrentStaffId);
        }

        return View(await records.OrderByDescending(record => record.CreatedAtUtc)
            .Take(200).ToListAsync());
    }

    public async Task<IActionResult> Create()
    {
        await PopulatePatientOptionsAsync();
        await PopulateDoctorOptionsAsync();
        await PopulateAppointmentOptionsAsync();
        return View(new MedicalRecordInput());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(MedicalRecordInput input)
    {
        await PopulatePatientOptionsAsync(input.PatientId);
        await PopulateDoctorOptionsAsync(input.DoctorId);
        await PopulateAppointmentOptionsAsync(input.AppointmentId);

        var doctorQuery = Db.Doctors.Where(doctor => doctor.Id == input.DoctorId && doctor.IsActive);
        if (User.IsInRole(StaffRoles.Doctor))
        {
            doctorQuery = doctorQuery.Where(doctor => doctor.StaffUserId == CurrentStaffId);
        }

        var patientExists = await Db.Patients.AnyAsync(patient =>
            patient.Id == input.PatientId && patient.IsActive);
        if (!ModelState.IsValid || !patientExists || !await doctorQuery.AnyAsync())
        {
            if (ModelState.IsValid)
            {
                ModelState.AddModelError(string.Empty,
                    "Select an active patient and a doctor account assigned to your role.");
            }

            return View(input);
        }

        if (input.AppointmentId.HasValue &&
            !await Db.Appointments.AnyAsync(appointment =>
                appointment.Id == input.AppointmentId.Value &&
                appointment.PatientId == input.PatientId &&
                appointment.DoctorId == input.DoctorId))
        {
            ModelState.AddModelError(nameof(input.AppointmentId),
                "The selected appointment must belong to this patient and doctor.");
            return View(input);
        }

        var record = new MedicalRecord
        {
            PatientId = input.PatientId,
            DoctorId = input.DoctorId,
            AppointmentId = input.AppointmentId,
            Notes = input.Notes.Trim()
        };
        Db.MedicalRecords.Add(record);
        await Db.SaveChangesAsync();
        await AddAuditAsync("Medical record created", "MedicalRecord", record.Id);
        TempData["Success"] = "Consultation record was saved.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = StaffRoles.Admin + "," + StaffRoles.Doctor)]
    public async Task<IActionResult> History(int patientId)
    {
        var records = Db.MedicalRecords.AsNoTracking()
            .Include(record => record.Patient)
            .Include(record => record.Doctor)
            .Where(record => record.PatientId == patientId);
        if (User.IsInRole(StaffRoles.Doctor))
        {
            records = records.Where(record => record.Doctor.StaffUserId == CurrentStaffId);
        }

        var patient = await Db.Patients.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == patientId);
        if (patient is null)
        {
            return NotFound();
        }

        ViewBag.Patient = patient;
        var result = await records.OrderByDescending(record => record.CreatedAtUtc).ToListAsync();
        await AddAuditAsync("Patient medical history viewed", "Patient", patientId);
        return View(result);
    }

    [Authorize(Roles = StaffRoles.Admin + "," + StaffRoles.Doctor)]
    public async Task<IActionResult> Edit(int id)
    {
        var record = await FindRecordAsync(id);
        return record is null
            ? NotFound()
            : View(new MedicalRecordEditInput { Notes = record.Notes });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = StaffRoles.Admin + "," + StaffRoles.Doctor)]
    public async Task<IActionResult> Edit(int id, MedicalRecordEditInput input)
    {
        if (!ModelState.IsValid)
        {
            return View(input);
        }

        var record = await FindRecordAsync(id);
        if (record is null)
        {
            return NotFound();
        }

        record.Notes = input.Notes.Trim();
        await Db.SaveChangesAsync();
        await AddAuditAsync("Medical record updated", "MedicalRecord", id);
        TempData["Success"] = "Clinical record was updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Details(int id)
    {
        var record = await FindRecordAsync(id, asNoTracking: true);
        if (record is null)
        {
            return NotFound();
        }

        await AddAuditAsync("Medical record viewed", "MedicalRecord", id);
        return View(record);
    }

    private IQueryable<MedicalRecord> ScopedRecords(bool asNoTracking = false)
    {
        var query = Db.MedicalRecords.AsQueryable();
        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        query = query.Include(record => record.Patient).Include(record => record.Doctor);
        if (User.IsInRole(StaffRoles.Doctor))
        {
            query = query.Where(record => record.Doctor.StaffUserId == CurrentStaffId);
        }

        return query;
    }

    private Task<MedicalRecord?> FindRecordAsync(int id, bool asNoTracking = false) =>
        ScopedRecords(asNoTracking).SingleOrDefaultAsync(record => record.Id == id);

    private async Task PopulateAppointmentOptionsAsync(int? selectedId = null)
    {
        var appointments = Db.Appointments.AsNoTracking()
            .Where(appointment => appointment.Status != AppointmentStatuses.Cancelled);
        if (User.IsInRole(StaffRoles.Doctor))
        {
            appointments = appointments.Where(appointment =>
                appointment.Doctor.StaffUserId == CurrentStaffId);
        }

        var items = await appointments.OrderByDescending(appointment => appointment.ScheduledAtUtc)
            .Take(100)
            .Select(appointment => new
            {
                appointment.Id,
                appointment.ScheduledAtUtc,
                PatientName = appointment.Patient.FullName,
                DoctorName = appointment.Doctor.FullName
            })
            .ToListAsync();
        ViewBag.Appointments = items.Select(appointment =>
            new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = appointment.Id.ToString(),
                Text = appointment.ScheduledAtUtc.ToLocalTime().ToString("g") + " · " +
                    appointment.PatientName + " · " + appointment.DoctorName,
                Selected = appointment.Id == selectedId
            }).ToList();
    }
}
