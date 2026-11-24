using System.Data;
using Microsoft.EntityFrameworkCore;
using schoolapp.Models;

namespace schoolapp.Data;

public static class HospitalDatabaseSmokeTest
{
    public static async Task RunAsync(ApplicationDbContext db)
    {
        if (!await db.Database.CanConnectAsync())
        {
            throw new InvalidOperationException("The configured hospital database is unavailable.");
        }

        var marker = Guid.NewGuid().ToString("N");
        var patientNumber = "SMOKE-" + marker[..16];
        var patientName = "Temporary HMS CRUD Patient " + marker[..8];
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        var staff = new StaffUser
        {
            Email = $"hms-smoke-{marker}@example.invalid",
            FullName = "Temporary HMS CRUD Test",
            PasswordHash = "smoke-test-hash",
            Role = StaffRoles.Doctor
        };
        var patient = new Patient
        {
            PatientNumber = patientNumber,
            FullName = patientName,
            DateOfBirth = new DateOnly(1990, 1, 1),
            Phone = "0000000000",
            Email = $"patient-{marker}@example.invalid"
        };
        var doctor = new Doctor
        {
            FullName = "Temporary HMS CRUD Doctor " + marker[..8],
            Specialty = "Smoke test",
            StaffUser = staff
        };

        db.StaffUsers.Add(staff);
        db.Patients.Add(patient);
        db.Doctors.Add(doctor);
        await db.SaveChangesAsync();

        var appointment = new Appointment
        {
            PatientId = patient.Id,
            DoctorId = doctor.Id,
            ScheduledAtUtc = DateTime.UtcNow.AddDays(30),
            DurationMinutes = 30,
            Status = AppointmentStatuses.Scheduled,
            Reason = "Temporary automated CRUD smoke test"
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();

        var record = new MedicalRecord
        {
            PatientId = patient.Id,
            DoctorId = doctor.Id,
            AppointmentId = appointment.Id,
            Notes = "Temporary note for an automated rollback-only database test."
        };
        var invoice = new Invoice
        {
            PatientId = patient.Id,
            AppointmentId = appointment.Id,
            Description = "Temporary automated CRUD test",
            TotalAmount = 100m,
            Status = "Unpaid"
        };
        db.MedicalRecords.Add(record);
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        var payment = new Payment
        {
            InvoiceId = invoice.Id,
            Amount = 25m,
            Method = "Cash",
            Reference = "AUTOMATED-ROLLBACK-TEST"
        };
        db.Payments.Add(payment);
        invoice.PaidAmount = payment.Amount;
        invoice.Status = "Partially paid";
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        var foundPatient = await db.Patients.SingleOrDefaultAsync(item =>
            item.PatientNumber == patientNumber);
        Ensure(foundPatient is not null, "Patient create/read failed.");
        Ensure(await db.Patients.AnyAsync(item =>
            item.FullName.Contains(patientName)), "Patient search by name failed.");
        Ensure(await db.Patients.AnyAsync(item =>
            item.PatientNumber == patientNumber), "Patient search by patient number failed.");

        foundPatient!.FullName = "Updated " + foundPatient.FullName;
        await db.SaveChangesAsync();
        Ensure(await db.Patients.AnyAsync(item =>
            item.Id == foundPatient.Id && item.FullName.StartsWith("Updated ")),
            "Patient update failed.");

        var foundDoctor = await db.Doctors.SingleAsync(item => item.Id == doctor.Id);
        foundDoctor.Specialty = "Updated smoke test";
        await db.SaveChangesAsync();
        Ensure(await db.Doctors.AnyAsync(item =>
            item.Id == doctor.Id && item.Specialty == "Updated smoke test"),
            "Doctor update failed.");

        var foundAppointment = await db.Appointments.SingleAsync(item => item.Id == appointment.Id);
        foundAppointment.Status = AppointmentStatuses.CheckedIn;
        await db.SaveChangesAsync();
        Ensure(await db.Appointments.AnyAsync(item =>
            item.Id == appointment.Id && item.Status == AppointmentStatuses.CheckedIn),
            "Appointment update failed.");

        var foundRecord = await db.MedicalRecords.SingleAsync(item => item.Id == record.Id);
        foundRecord.Notes = "Updated temporary smoke-test note.";
        await db.SaveChangesAsync();
        Ensure(await db.MedicalRecords.AnyAsync(item =>
            item.Id == record.Id && item.Notes == "Updated temporary smoke-test note."),
            "Medical-record update failed.");

        var foundInvoice = await db.Invoices.SingleAsync(item => item.Id == invoice.Id);
        Ensure(await db.Payments.AnyAsync(item =>
            item.InvoiceId == invoice.Id && item.Amount == 25m),
            "Payment-history read failed.");
        Ensure(foundInvoice.PaidAmount == 25m && foundInvoice.Status == "Partially paid",
            "Invoice payment update failed.");

        db.Payments.Remove(await db.Payments.SingleAsync(item => item.Id == payment.Id));
        db.MedicalRecords.Remove(await db.MedicalRecords.SingleAsync(item => item.Id == record.Id));
        db.Invoices.Remove(await db.Invoices.SingleAsync(item => item.Id == invoice.Id));
        db.Appointments.Remove(await db.Appointments.SingleAsync(item => item.Id == appointment.Id));
        db.Patients.Remove(await db.Patients.SingleAsync(item => item.Id == patient.Id));
        db.Doctors.Remove(await db.Doctors.SingleAsync(item => item.Id == doctor.Id));
        db.StaffUsers.Remove(await db.StaffUsers.SingleAsync(item => item.Id == staff.Id));
        await db.SaveChangesAsync();
        Ensure(!await db.Patients.AnyAsync(item => item.PatientNumber == patientNumber),
            "Patient delete failed.");
        Ensure(!await db.Doctors.AnyAsync(item => item.Id == doctor.Id), "Doctor delete failed.");
        Ensure(!await db.Appointments.AnyAsync(item => item.Id == appointment.Id),
            "Appointment delete failed.");
        Ensure(!await db.MedicalRecords.AnyAsync(item => item.Id == record.Id),
            "Medical-record delete failed.");
        Ensure(!await db.Invoices.AnyAsync(item => item.Id == invoice.Id),
            "Invoice delete failed.");
        Ensure(!await db.Payments.AnyAsync(item => item.Id == payment.Id), "Payment delete failed.");

        await transaction.RollbackAsync();
        db.ChangeTracker.Clear();
        Ensure(!await db.Patients.AnyAsync(item => item.PatientNumber == patientNumber),
            "The temporary patient was not rolled back.");
        Ensure(!await db.StaffUsers.AnyAsync(item => item.Email == staff.Email),
            "The temporary staff account was not rolled back.");
        Console.WriteLine("HMS database CRUD smoke test passed. Temporary test records were rolled back.");
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
