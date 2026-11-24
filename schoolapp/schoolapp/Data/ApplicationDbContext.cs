using Microsoft.EntityFrameworkCore;
using schoolapp.Models;

namespace schoolapp.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options)
{
    public DbSet<Student> Students => Set<Student>();
    public DbSet<StaffUser> StaffUsers => Set<StaffUser>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<MedicalRecord> MedicalRecords => Set<MedicalRecord>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<StaffUser>()
            .HasIndex(user => user.Email)
            .IsUnique();
        modelBuilder.Entity<Patient>()
            .HasIndex(patient => patient.PatientNumber)
            .IsUnique();
        modelBuilder.Entity<Patient>()
            .HasIndex(patient => patient.FullName);
        modelBuilder.Entity<Appointment>()
            .HasIndex(appointment => new { appointment.DoctorId, appointment.ScheduledAtUtc });
        modelBuilder.Entity<Doctor>()
            .HasIndex(doctor => doctor.StaffUserId)
            .IsUnique();
        modelBuilder.Entity<Doctor>()
            .HasOne(doctor => doctor.StaffUser)
            .WithMany()
            .HasForeignKey(doctor => doctor.StaffUserId)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Appointment>()
            .HasOne(appointment => appointment.Patient)
            .WithMany(patient => patient.Appointments)
            .HasForeignKey(appointment => appointment.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Appointment>()
            .HasOne(appointment => appointment.Doctor)
            .WithMany(doctor => doctor.Appointments)
            .HasForeignKey(appointment => appointment.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<MedicalRecord>()
            .HasOne(record => record.Patient)
            .WithMany(patient => patient.MedicalRecords)
            .HasForeignKey(record => record.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<MedicalRecord>()
            .HasOne(record => record.Doctor)
            .WithMany(doctor => doctor.MedicalRecords)
            .HasForeignKey(record => record.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<MedicalRecord>()
            .HasOne(record => record.Appointment)
            .WithMany(appointment => appointment.MedicalRecords)
            .HasForeignKey(record => record.AppointmentId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Invoice>()
            .Property(invoice => invoice.TotalAmount)
            .HasPrecision(12, 2);
        modelBuilder.Entity<Invoice>()
            .Property(invoice => invoice.PaidAmount)
            .HasPrecision(12, 2);
        modelBuilder.Entity<Invoice>()
            .HasOne(invoice => invoice.Patient)
            .WithMany(patient => patient.Invoices)
            .HasForeignKey(invoice => invoice.PatientId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Invoice>()
            .HasOne(invoice => invoice.Appointment)
            .WithMany(appointment => appointment.Invoices)
            .HasForeignKey(invoice => invoice.AppointmentId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Payment>()
            .Property(payment => payment.Amount)
            .HasPrecision(12, 2);
        modelBuilder.Entity<AuditEvent>()
            .HasOne(audit => audit.StaffUser)
            .WithMany()
            .HasForeignKey(audit => audit.StaffUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}