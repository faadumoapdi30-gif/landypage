using System.ComponentModel.DataAnnotations;

namespace schoolapp.Models;

public sealed class StaffUser
{
    public int Id { get; set; }

    [Required, StringLength(160), EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Required, StringLength(30)]
    public string Role { get; set; } = StaffRoles.Receptionist;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public static class StaffRoles
{
    public const string Admin = "Admin";
    public const string Receptionist = "Receptionist";
    public const string Doctor = "Doctor";
    public const string Billing = "Billing";
}

public sealed class Patient
{
    public int Id { get; set; }

    [Required, StringLength(24)]
    public string PatientNumber { get; set; } = string.Empty;

    [Required, StringLength(120)]
    public string FullName { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    public DateOnly DateOfBirth { get; set; }

    [StringLength(20)]
    public string Gender { get; set; } = string.Empty;

    [StringLength(30), Phone]
    public string Phone { get; set; } = string.Empty;

    [StringLength(160), EmailAddress]
    public string Email { get; set; } = string.Empty;

    [StringLength(300)]
    public string Address { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    public ICollection<MedicalRecord> MedicalRecords { get; set; } = new List<MedicalRecord>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}

public sealed class Doctor
{
    public int Id { get; set; }
    public int? StaffUserId { get; set; }
    public StaffUser? StaffUser { get; set; }

    [Required, StringLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Specialty { get; set; } = string.Empty;

    [StringLength(30), Phone]
    public string Phone { get; set; } = string.Empty;

    [StringLength(160), EmailAddress]
    public string Email { get; set; } = string.Empty;

    [StringLength(80)]
    public string LicenseReference { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    public ICollection<MedicalRecord> MedicalRecords { get; set; } = new List<MedicalRecord>();
}

public static class AppointmentStatuses
{
    public const string Scheduled = "Scheduled";
    public const string CheckedIn = "Checked in";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All = [Scheduled, CheckedIn, Completed, Cancelled];
}

public sealed class Appointment
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public Patient Patient { get; set; } = null!;
    public int DoctorId { get; set; }
    public Doctor Doctor { get; set; } = null!;

    [Required]
    public DateTime ScheduledAtUtc { get; set; }

    public int DurationMinutes { get; set; } = 30;

    [Required, StringLength(30)]
    public string Status { get; set; } = AppointmentStatuses.Scheduled;

    [StringLength(300)]
    public string Reason { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<MedicalRecord> MedicalRecords { get; set; } = new List<MedicalRecord>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}

public sealed class MedicalRecord
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public Patient Patient { get; set; } = null!;
    public int DoctorId { get; set; }
    public Doctor Doctor { get; set; } = null!;
    public int? AppointmentId { get; set; }
    public Appointment? Appointment { get; set; }

    [Required, StringLength(6000)]
    public string Notes { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class Invoice
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public Patient Patient { get; set; } = null!;
    public int? AppointmentId { get; set; }
    public Appointment? Appointment { get; set; }

    [Required, StringLength(240)]
    public string Description { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "999999999")]
    public decimal TotalAmount { get; set; }

    public decimal PaidAmount { get; set; }

    [Required, StringLength(20)]
    public string Status { get; set; } = "Unpaid";

    public DateTime IssuedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

public sealed class Payment
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;

    [Range(typeof(decimal), "0.01", "999999999")]
    public decimal Amount { get; set; }

    [Required, StringLength(30)]
    public string Method { get; set; } = "Cash";

    [StringLength(100)]
    public string Reference { get; set; } = string.Empty;

    public DateTime PaidAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class AuditEvent
{
    public int Id { get; set; }
    public int? StaffUserId { get; set; }
    public StaffUser? StaffUser { get; set; }

    [Required, StringLength(80)]
    public string Action { get; set; } = string.Empty;

    [Required, StringLength(80)]
    public string EntityType { get; set; } = string.Empty;

    public int? EntityId { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}
