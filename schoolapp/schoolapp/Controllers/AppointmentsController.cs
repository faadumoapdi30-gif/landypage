using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using schoolapp.Data;
using schoolapp.Models;

namespace schoolapp.Controllers;

[Authorize(Roles = StaffRoles.Admin + "," + StaffRoles.Receptionist + "," + StaffRoles.Doctor)]
public sealed class AppointmentsController(
    ApplicationDbContext db,
    IConfiguration configuration) : HmsController(db, configuration)
{
    private const int DefaultAppointmentDurationMinutes = 30;

    public async Task<IActionResult> Index(string? status)
    {
        var appointments = Db.Appointments.AsNoTracking()
            .Include(appointment => appointment.Patient)
            .Include(appointment => appointment.Doctor)
            .AsQueryable();
        if (User.IsInRole(StaffRoles.Doctor))
        {
            appointments = appointments.Where(appointment =>
                appointment.Doctor.StaffUserId == CurrentStaffId);
        }

        if (!string.IsNullOrWhiteSpace(status) && AppointmentStatuses.All.Contains(status))
        {
            appointments = appointments.Where(appointment => appointment.Status == status);
        }

        ViewBag.Status = status;
        return View(await appointments.OrderByDescending(appointment => appointment.ScheduledAtUtc)
            .Take(250).ToListAsync());
    }

    public async Task<IActionResult> Create()
    {
        await PopulatePatientOptionsAsync();
        await PopulateDoctorOptionsAsync();
        return View(new AppointmentInput());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AppointmentInput input)
    {
        await PopulatePatientOptionsAsync(input.PatientId);
        await PopulateDoctorOptionsAsync(input.DoctorId);
        if (!ModelState.IsValid)
        {
            return View(input);
        }

        var scheduledUtc = new DateTimeOffset(input.ScheduledAtLocal).UtcDateTime;
        if (scheduledUtc <= DateTime.UtcNow)
        {
            ModelState.AddModelError(nameof(input.ScheduledAtLocal), "Choose a future appointment time.");
            return View(input);
        }

        var patientExists = await Db.Patients.AnyAsync(patient =>
            patient.Id == input.PatientId && patient.IsActive);
        var doctorQuery = Db.Doctors.Where(doctor => doctor.Id == input.DoctorId && doctor.IsActive);
        if (User.IsInRole(StaffRoles.Doctor))
        {
            doctorQuery = doctorQuery.Where(doctor => doctor.StaffUserId == CurrentStaffId);
        }

        if (!patientExists || !await doctorQuery.AnyAsync())
        {
            ModelState.AddModelError(string.Empty, "Select an active patient and an authorized active doctor.");
            return View(input);
        }

        await using var transaction = await Db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var conflict = await Db.Appointments.AnyAsync(appointment =>
            appointment.DoctorId == input.DoctorId &&
            appointment.ScheduledAtUtc < scheduledUtc.AddMinutes(DefaultAppointmentDurationMinutes) &&
            appointment.ScheduledAtUtc > scheduledUtc.AddMinutes(-DefaultAppointmentDurationMinutes) &&
            appointment.Status != AppointmentStatuses.Cancelled);
        if (conflict)
        {
            ModelState.AddModelError(nameof(input.ScheduledAtLocal),
                "This doctor already has an appointment at that time.");
            await transaction.RollbackAsync();
            return View(input);
        }

        var appointment = new Appointment
        {
            PatientId = input.PatientId,
            DoctorId = input.DoctorId,
            ScheduledAtUtc = scheduledUtc,
            DurationMinutes = DefaultAppointmentDurationMinutes,
            Status = AppointmentStatuses.Scheduled,
            Reason = input.Reason.Trim()
        };
        Db.Appointments.Add(appointment);
        await Db.SaveChangesAsync();
        await AddAuditAsync("Appointment created", "Appointment", appointment.Id);
        await transaction.CommitAsync();
        TempData["Success"] = "Appointment was scheduled.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var appointment = await FindAppointmentAsync(id);
        if (appointment is null)
        {
            return NotFound();
        }

        if (appointment.Status != AppointmentStatuses.Scheduled)
        {
            TempData["Error"] = "Only scheduled appointments can be edited.";
            return RedirectToAction(nameof(Index));
        }

        await PopulatePatientOptionsAsync(appointment.PatientId);
        await PopulateDoctorOptionsAsync(appointment.DoctorId);
        return View(new AppointmentInput
        {
            Id = appointment.Id,
            PatientId = appointment.PatientId,
            DoctorId = appointment.DoctorId,
            ScheduledAtLocal = appointment.ScheduledAtUtc.ToLocalTime(),
            Reason = appointment.Reason
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AppointmentInput input)
    {
        if (id != input.Id)
        {
            return BadRequest();
        }

        await PopulatePatientOptionsAsync(input.PatientId);
        await PopulateDoctorOptionsAsync(input.DoctorId);
        if (!ModelState.IsValid)
        {
            return View(input);
        }

        var appointment = await FindAppointmentAsync(id);
        if (appointment is null)
        {
            return NotFound();
        }

        if (appointment.Status != AppointmentStatuses.Scheduled)
        {
            return BadRequest("Only scheduled appointments can be edited.");
        }

        var scheduledUtc = new DateTimeOffset(input.ScheduledAtLocal).UtcDateTime;
        var patientExists = await Db.Patients.AnyAsync(patient =>
            patient.Id == input.PatientId && patient.IsActive);
        var doctorQuery = Db.Doctors.Where(doctor => doctor.Id == input.DoctorId && doctor.IsActive);
        if (User.IsInRole(StaffRoles.Doctor))
        {
            doctorQuery = doctorQuery.Where(doctor => doctor.StaffUserId == CurrentStaffId);
        }

        if (scheduledUtc <= DateTime.UtcNow || !patientExists || !await doctorQuery.AnyAsync())
        {
            ModelState.AddModelError(string.Empty, "Choose a future time, active patient and authorized doctor.");
            return View(input);
        }

        var conflict = await Db.Appointments.AnyAsync(other =>
            other.Id != id && other.DoctorId == input.DoctorId &&
            other.ScheduledAtUtc < scheduledUtc.AddMinutes(DefaultAppointmentDurationMinutes) &&
            other.ScheduledAtUtc > scheduledUtc.AddMinutes(-DefaultAppointmentDurationMinutes) &&
            other.Status != AppointmentStatuses.Cancelled);
        if (conflict)
        {
            ModelState.AddModelError(nameof(input.ScheduledAtLocal),
                "This doctor already has an appointment at that time.");
            return View(input);
        }

        appointment.PatientId = input.PatientId;
        appointment.DoctorId = input.DoctorId;
        appointment.ScheduledAtUtc = scheduledUtc;
        appointment.Reason = input.Reason.Trim();
        await Db.SaveChangesAsync();
        await AddAuditAsync("Appointment updated", "Appointment", id);
        TempData["Success"] = "Appointment was updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, string status)
    {
        if (!AppointmentStatuses.All.Contains(status))
        {
            return BadRequest("Unknown appointment status.");
        }

        var appointment = await FindAppointmentAsync(id);
        if (appointment is null)
        {
            return NotFound();
        }

        var transitionAllowed = appointment.Status switch
        {
            AppointmentStatuses.Scheduled => status is AppointmentStatuses.CheckedIn or AppointmentStatuses.Cancelled,
            AppointmentStatuses.CheckedIn => status is AppointmentStatuses.Completed or AppointmentStatuses.Cancelled,
            _ => false
        };
        if (!transitionAllowed)
        {
            TempData["Error"] = "That appointment status change is not allowed.";
            return RedirectToAction(nameof(Index));
        }

        appointment.Status = status;
        await Db.SaveChangesAsync();
        await AddAuditAsync("Appointment status changed to " + status, "Appointment", id);
        TempData["Success"] = "Appointment status was updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = StaffRoles.Admin + "," + StaffRoles.Receptionist)]
    public async Task<IActionResult> Delete(int id)
    {
        var appointment = await FindAppointmentAsync(id);
        if (appointment is null)
        {
            return NotFound();
        }

        if (appointment.Status != AppointmentStatuses.Scheduled ||
            await Db.MedicalRecords.AnyAsync(record => record.AppointmentId == id) ||
            await Db.Invoices.AnyAsync(invoice => invoice.AppointmentId == id))
        {
            TempData["Error"] = "Only a scheduled appointment without clinical or billing history can be deleted. Cancel the appointment to preserve its history.";
            return RedirectToAction(nameof(Index));
        }

        Db.Appointments.Remove(appointment);
        await Db.SaveChangesAsync();
        await AddAuditAsync("Appointment deleted", "Appointment", id);
        TempData["Success"] = "Appointment was deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<Appointment?> FindAppointmentAsync(int id)
    {
        var query = Db.Appointments.Include(appointment => appointment.Patient)
            .Include(appointment => appointment.Doctor).AsQueryable();
        if (User.IsInRole(StaffRoles.Doctor))
        {
            query = query.Where(appointment => appointment.Doctor.StaffUserId == CurrentStaffId);
        }

        return await query.SingleOrDefaultAsync(appointment => appointment.Id == id);
    }
}
