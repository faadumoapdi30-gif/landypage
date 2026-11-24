using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using schoolapp.Data;
using schoolapp.Models;

namespace schoolapp.Controllers;

[Authorize]
public sealed class DashboardController(
    ApplicationDbContext db,
    IConfiguration configuration,
    ILogger<DashboardController> logger) : HmsController(db, configuration)
{
    public async Task<IActionResult> Index()
    {
        var model = new DashboardViewModel();
        if (!IsDatabaseConfigured)
        {
            return View(model with
            {
                DatabaseReady = false,
                DatabaseMessage = "The Supabase/PostgreSQL connection has not been configured."
            });
        }

        try
        {
            if (!await Db.Database.CanConnectAsync())
            {
                return View(model with
                {
                    DatabaseMessage = "The hospital database could not be reached. Check the connection settings."
                });
            }

            var localToday = DateTime.Today;
            var doctorQuery = Db.Doctors.AsNoTracking();
            var appointmentQuery = Db.Appointments.AsNoTracking();
            if (User.IsInRole(StaffRoles.Doctor))
            {
                doctorQuery = doctorQuery.Where(doctor => doctor.StaffUserId == CurrentStaffId);
                appointmentQuery = appointmentQuery.Where(appointment =>
                    appointment.Doctor.StaffUserId == CurrentStaffId);
            }

            model = new DashboardViewModel
            {
                DatabaseReady = true,
                PatientCount = await Db.Patients.CountAsync(patient => patient.IsActive),
                DoctorCount = await doctorQuery.CountAsync(doctor => doctor.IsActive),
                TotalAppointmentCount = await appointmentQuery.CountAsync(),
                TodayAppointmentCount = await appointmentQuery.CountAsync(appointment =>
                    appointment.ScheduledAtUtc >= localToday.ToUniversalTime() &&
                    appointment.ScheduledAtUtc < localToday.AddDays(1).ToUniversalTime() &&
                    appointment.Status != AppointmentStatuses.Cancelled),
                PendingInvoiceCount = User.IsInRole(StaffRoles.Admin) || User.IsInRole(StaffRoles.Billing)
                    ? await Db.Invoices.CountAsync(invoice => invoice.Status != "Paid")
                    : 0,
                OutstandingBalance = User.IsInRole(StaffRoles.Admin) || User.IsInRole(StaffRoles.Billing)
                    ? await Db.Invoices.Where(invoice => invoice.Status != "Paid")
                        .SumAsync(invoice => (decimal?)(invoice.TotalAmount - invoice.PaidAmount)) ?? 0
                    : 0,
                UpcomingAppointments = await appointmentQuery
                    .Where(appointment => appointment.ScheduledAtUtc >= DateTime.UtcNow &&
                        appointment.Status != AppointmentStatuses.Cancelled)
                    .OrderBy(appointment => appointment.ScheduledAtUtc)
                    .Take(6)
                    .Include(appointment => appointment.Patient)
                    .Include(appointment => appointment.Doctor)
                    .ToListAsync()
            };
        }
        catch (Npgsql.NpgsqlException exception)
        {
            logger.LogError(exception, "Could not load the HMS dashboard.");
            model = model with
            {
                DatabaseReady = false,
                DatabaseMessage = "The database is reachable but its HMS schema is unavailable. Apply the database migrations."
            };
        }

        return View(model);
    }
}
