using Microsoft.AspNetCore.Mvc;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Controllers;

public class StudentsController : Controller
{
    private readonly IStudentRepository _students;

    public StudentsController(IStudentRepository students)
    {
        _students = students;
    }

    public IActionResult Index(string? search)
    {
        ViewData["Search"] = search;
        return View(_students.GetAll(search));
    }

    public IActionResult Details(int id)
    {
        var student = _students.GetById(id);
        return student is null ? NotFound() : View(student);
    }

    public IActionResult Create()
    {
        return View(new Student { EnrollmentDate = DateTime.Today });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(Student student)
    {
        if (_students.EmailExists(student.Email))
        {
            ModelState.AddModelError(nameof(Student.Email), "A student with this email already exists.");
        }

        if (!ModelState.IsValid)
        {
            return View(student);
        }

        _students.Add(student);
        TempData["Success"] = $"{student.FullName} was added successfully.";
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Edit(int id)
    {
        var student = _students.GetById(id);
        return student is null ? NotFound() : View(student);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, Student student)
    {
        if (id != student.Id)
        {
            return BadRequest();
        }

        if (_students.EmailExists(student.Email, student.Id))
        {
            ModelState.AddModelError(nameof(Student.Email), "Another student already uses this email.");
        }

        if (!ModelState.IsValid)
        {
            return View(student);
        }

        if (!_students.Update(student))
        {
            return NotFound();
        }

        TempData["Success"] = $"{student.FullName} was updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Delete(int id)
    {
        var student = _students.GetById(id);
        return student is null ? NotFound() : View(student);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteConfirmed(int id)
    {
        var student = _students.GetById(id);
        if (student is null)
        {
            return NotFound();
        }

        _students.Delete(id);
        TempData["Success"] = $"{student.FullName} was deleted.";
        return RedirectToAction(nameof(Index));
    }
}
