using Microsoft.AspNetCore.Mvc;

namespace schoolapp.Controllers;

public sealed class ProjectReportController : Controller
{
    public IActionResult Index()
    {
        ViewData["Title"] = "Academic Project Report";
        ViewData["IsProjectReport"] = true;
        return View("~/Views/Home/Index.cshtml");
    }
}
