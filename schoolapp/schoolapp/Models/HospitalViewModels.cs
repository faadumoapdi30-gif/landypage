using System.ComponentModel.DataAnnotations;

namespace schoolapp.Models;

public sealed class StaffSetupInput
{
    [Required, StringLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Required, StringLength(160), EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(12), DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required, Compare(nameof(Password)), DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class LoginInput
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public sealed class StaffCreateInput
{
    [Required, StringLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Required, StringLength(160), EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(12), DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = StaffRoles.Receptionist;
}

public sealed class ChangePasswordInput
{
    [Required, DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, MinLength(12), DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Required, Compare(nameof(NewPassword)), DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class AppointmentInput
{
    public int Id { get; set; }

    [Range(1, int.MaxValue)]
    public int PatientId { get; set; }

    [Range(1, int.MaxValue)]
    public int DoctorId { get; set; }

    [Required, DataType(DataType.DateTime)]
    public DateTime ScheduledAtLocal { get; set; } = DateTime.Now.AddHours(1);

    [StringLength(300)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class MedicalRecordInput
{
    [Range(1, int.MaxValue)]
    public int PatientId { get; set; }

    [Range(1, int.MaxValue)]
    public int DoctorId { get; set; }

    public int? AppointmentId { get; set; }

    [Required, StringLength(6000, MinimumLength = 3)]
    public string Notes { get; set; } = string.Empty;
}

public sealed class MedicalRecordEditInput
{
    [Required, StringLength(6000, MinimumLength = 3)]
    public string Notes { get; set; } = string.Empty;
}

public sealed class InvoiceInput
{
    [Range(1, int.MaxValue)]
    public int PatientId { get; set; }

    public int? AppointmentId { get; set; }

    [Required, StringLength(240)]
    public string Description { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "999999999")]
    public decimal TotalAmount { get; set; }
}

public sealed class PaymentInput
{
    [Range(typeof(decimal), "0.01", "999999999")]
    public decimal Amount { get; set; }

    [Required, StringLength(30)]
    public string Method { get; set; } = "Cash";

    [StringLength(100)]
    public string Reference { get; set; } = string.Empty;
}

public sealed class DoctorInput
{
    public int? StaffUserId { get; set; }

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
}

public sealed record DashboardViewModel
{
    public int PatientCount { get; init; }
    public int DoctorCount { get; init; }
    public int TotalAppointmentCount { get; init; }
    public int TodayAppointmentCount { get; init; }
    public int PendingInvoiceCount { get; init; }
    public decimal OutstandingBalance { get; init; }
    public IReadOnlyList<Appointment> UpcomingAppointments { get; init; } = [];
    public bool DatabaseReady { get; init; }
    public string? DatabaseMessage { get; init; }
}
