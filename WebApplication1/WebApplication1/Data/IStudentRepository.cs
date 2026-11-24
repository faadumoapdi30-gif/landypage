using WebApplication1.Models;

namespace WebApplication1.Data;

/// <summary>
/// Storage abstraction for students. Swap the in-memory implementation for a
/// Supabase-backed one later without changing the controller or views.
/// </summary>
public interface IStudentRepository
{
    IReadOnlyList<Student> GetAll(string? search = null);

    Student? GetById(int id);

    Student Add(Student student);

    bool Update(Student student);

    bool Delete(int id);

    bool EmailExists(string email, int? excludeStudentId = null);
}
