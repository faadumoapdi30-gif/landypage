using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models;

public class Student
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(100)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Phone]
    [StringLength(20)]
    [Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "Course is required.")]
    [StringLength(100)]
    public string Course { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    [Display(Name = "Enrollment date")]
    public DateTime EnrollmentDate { get; set; } = DateTime.Today;

    [Range(0, 4, ErrorMessage = "GPA must be between 0 and 4.")]
    [Display(Name = "GPA")]
    public decimal? Gpa { get; set; }
}
