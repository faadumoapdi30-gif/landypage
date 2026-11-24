using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using schoolapp.Data;
using schoolapp.Models;

namespace schoolapp.Controllers;

public abstract class HmsController(
    ApplicationDbContext db,
    IConfiguration configuration) : Controller
{
    protected ApplicationDbContext Db { get; } = db;
    protected bool IsDatabaseConfigured =>
        !string.IsNullOrWhiteSpace(configuration.GetConnectionString("Supabase"));

    protected int CurrentStaffId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    protected async Task AddAuditAsync(string action, string entityType, int? entityId)
    {
        Db.AuditEvents.Add(new AuditEvent
        {
            StaffUserId = CurrentStaffId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OccurredAtUtc = DateTime.UtcNow
        });
        await Db.SaveChangesAsync();
    }

    protected async Task PopulatePatientOptionsAsync(int? selectedId = null)
    {
        ViewBag.Patients = await Db.Patients.AsNoTracking()
            .Where(patient => patient.IsActive)
            .OrderBy(patient => patient.FullName)
            .Select(patient => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = patient.Id.ToString(),
                Text = patient.PatientNumber + " · " + patient.FullName,
                Selected = patient.Id == selectedId
            })
            .ToListAsync();
    }

    protected async Task PopulateDoctorOptionsAsync(int? selectedId = null)
    {
        var doctors = Db.Doctors.AsNoTracking().Where(doctor => doctor.IsActive);
        if (User.IsInRole(StaffRoles.Doctor))
        {
            doctors = doctors.Where(doctor => doctor.StaffUserId == CurrentStaffId);
        }

        ViewBag.Doctors = await doctors.OrderBy(doctor => doctor.FullName)
            .Select(doctor => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = doctor.Id.ToString(),
                Text = doctor.FullName + " · " + doctor.Specialty,
                Selected = doctor.Id == selectedId
            })
            .ToListAsync();
    }
}
