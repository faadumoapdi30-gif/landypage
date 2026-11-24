using WebApplication1.Models;

namespace WebApplication1.Data;

/// <summary>
/// Temporary store so the CRUD works before Supabase is connected.
/// Data resets whenever the application restarts.
/// </summary>
public sealed class InMemoryStudentRepository : IStudentRepository
{
    private readonly object _gate = new();
    private readonly List<Student> _students;
    private int _nextId;

    public InMemoryStudentRepository()
    {
        _students =
        [
            new Student
            {
                Id = 1,
                FullName = "Amina Yusuf",
                Email = "amina.yusuf@example.com",
                PhoneNumber = "+252 61 555 0101",
                Course = "Computer Science",
                EnrollmentDate = new DateTime(2025, 9, 1),
                Gpa = 3.7m
            },
            new Student
            {
                Id = 2,
                FullName = "Mohamed Ali",
                Email = "mohamed.ali@example.com",
                PhoneNumber = "+252 61 555 0102",
                Course = "Software Engineering",
                EnrollmentDate = new DateTime(2025, 9, 1),
                Gpa = 3.2m
            },
            new Student
            {
                Id = 3,
                FullName = "Sara Hassan",
                Email = "sara.hassan@example.com",
                Course = "Information Systems",
                EnrollmentDate = new DateTime(2026, 1, 12),
                Gpa = 3.9m
            }
        ];
        _nextId = _students.Max(s => s.Id) + 1;
    }

    public IReadOnlyList<Student> GetAll(string? search = null)
    {
        lock (_gate)
        {
            IEnumerable<Student> query = _students;

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(s =>
                    s.FullName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    s.Email.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    s.Course.Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            return query
                .OrderBy(s => s.FullName)
                .Select(Clone)
                .ToList();
        }
    }

    public Student? GetById(int id)
    {
        lock (_gate)
        {
            var student = _students.FirstOrDefault(s => s.Id == id);
            return student is null ? null : Clone(student);
        }
    }

    public Student Add(Student student)
    {
        lock (_gate)
        {
            var created = Clone(student);
            created.Id = _nextId++;
            _students.Add(created);
            return Clone(created);
        }
    }

    public bool Update(Student student)
    {
        lock (_gate)
        {
            var existing = _students.FirstOrDefault(s => s.Id == student.Id);
            if (existing is null)
            {
                return false;
            }

            existing.FullName = student.FullName;
            existing.Email = student.Email;
            existing.PhoneNumber = student.PhoneNumber;
            existing.Course = student.Course;
            existing.EnrollmentDate = student.EnrollmentDate;
            existing.Gpa = student.Gpa;
            return true;
        }
    }

    public bool Delete(int id)
    {
        lock (_gate)
        {
            var student = _students.FirstOrDefault(s => s.Id == id);
            return student is not null && _students.Remove(student);
        }
    }

    public bool EmailExists(string email, int? excludeStudentId = null)
    {
        lock (_gate)
        {
            return _students.Any(s =>
                s.Id != excludeStudentId &&
                string.Equals(s.Email, email?.Trim(), StringComparison.OrdinalIgnoreCase));
        }
    }

    private static Student Clone(Student student) => new()
    {
        Id = student.Id,
        FullName = student.FullName,
        Email = student.Email,
        PhoneNumber = student.PhoneNumber,
        Course = student.Course,
        EnrollmentDate = student.EnrollmentDate,
        Gpa = student.Gpa
    };
}
