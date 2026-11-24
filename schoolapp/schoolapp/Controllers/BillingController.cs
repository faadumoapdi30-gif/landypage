using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using schoolapp.Data;
using schoolapp.Models;

namespace schoolapp.Controllers;

[Authorize(Roles = StaffRoles.Admin + "," + StaffRoles.Billing)]
public sealed class BillingController(
    ApplicationDbContext db,
    IConfiguration configuration) : HmsController(db, configuration)
{
    public async Task<IActionResult> Index(string? status)
    {
        var invoices = Db.Invoices.AsNoTracking().Include(invoice => invoice.Patient)
            .AsQueryable();
        if (status is "Unpaid" or "Partially paid" or "Paid")
        {
            invoices = invoices.Where(invoice => invoice.Status == status);
        }

        ViewBag.Status = status;
        ViewBag.Outstanding = await Db.Invoices
            .Where(invoice => invoice.Status != "Paid")
            .SumAsync(invoice => (decimal?)(invoice.TotalAmount - invoice.PaidAmount)) ?? 0;
        return View(await invoices.OrderByDescending(invoice => invoice.IssuedAtUtc)
            .Take(250).ToListAsync());
    }

    public async Task<IActionResult> Create()
    {
        await PopulatePatientOptionsAsync();
        return View(new InvoiceInput());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(InvoiceInput input)
    {
        await PopulatePatientOptionsAsync(input.PatientId);
        if (!ModelState.IsValid)
        {
            return View(input);
        }

        if (!await Db.Patients.AnyAsync(patient => patient.Id == input.PatientId && patient.IsActive))
        {
            ModelState.AddModelError(nameof(input.PatientId), "Select an active patient.");
            return View(input);
        }

        if (input.AppointmentId.HasValue &&
            !await Db.Appointments.AnyAsync(appointment =>
                appointment.Id == input.AppointmentId.Value && appointment.PatientId == input.PatientId))
        {
            ModelState.AddModelError(nameof(input.AppointmentId),
                "The selected appointment does not belong to this patient.");
            return View(input);
        }

        var invoice = new Invoice
        {
            PatientId = input.PatientId,
            AppointmentId = input.AppointmentId,
            Description = input.Description.Trim(),
            TotalAmount = decimal.Round(input.TotalAmount, 2),
            Status = "Unpaid"
        };
        Db.Invoices.Add(invoice);
        await Db.SaveChangesAsync();
        await AddAuditAsync("Invoice created", "Invoice", invoice.Id);
        TempData["Success"] = "Invoice was created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Payment(int id)
    {
        var invoice = await Db.Invoices.AsNoTracking()
            .Include(item => item.Patient)
            .SingleOrDefaultAsync(item => item.Id == id);
        if (invoice is null)
        {
            return NotFound();
        }

        ViewBag.Invoice = invoice;
        return View(new PaymentInput { Amount = invoice.TotalAmount - invoice.PaidAmount });
    }

    public async Task<IActionResult> Details(int id)
    {
        var invoice = await Db.Invoices.AsNoTracking()
            .Include(item => item.Patient)
            .Include(item => item.Payments)
            .SingleOrDefaultAsync(item => item.Id == id);
        return invoice is null ? NotFound() : View(invoice);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Payment(int id, PaymentInput input)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Invoice = await Db.Invoices.AsNoTracking()
                .Include(item => item.Patient)
                .SingleOrDefaultAsync(item => item.Id == id);
            if (ViewBag.Invoice is null)
            {
                return NotFound();
            }

            return View(input);
        }

        if (input.Method is not ("Cash" or "Card terminal" or "Bank transfer" or "Insurance"))
        {
            ModelState.AddModelError(nameof(input.Method), "Choose a supported payment method.");
            ViewBag.Invoice = await Db.Invoices.AsNoTracking()
                .Include(item => item.Patient)
                .SingleOrDefaultAsync(item => item.Id == id);
            if (ViewBag.Invoice is null)
            {
                return NotFound();
            }

            return View(input);
        }

        await using var transaction = await Db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var invoice = await Db.Invoices.Include(item => item.Patient)
            .SingleOrDefaultAsync(item => item.Id == id);
        if (invoice is null)
        {
            await transaction.RollbackAsync();
            return NotFound();
        }

        ViewBag.Invoice = invoice;
        var balance = invoice.TotalAmount - invoice.PaidAmount;
        if (decimal.Round(input.Amount, 2) > balance)
        {
            ModelState.AddModelError(nameof(input.Amount), "Payment cannot exceed the outstanding balance.");
            await transaction.RollbackAsync();
            return View(input);
        }

        invoice.PaidAmount += decimal.Round(input.Amount, 2);
        invoice.Status = invoice.PaidAmount >= invoice.TotalAmount
            ? "Paid"
            : "Partially paid";
        Db.Payments.Add(new Payment
        {
            InvoiceId = invoice.Id,
            Amount = decimal.Round(input.Amount, 2),
            Method = input.Method,
            Reference = input.Reference.Trim()
        });
        await Db.SaveChangesAsync();
        await AddAuditAsync("Payment recorded", "Invoice", invoice.Id);
        await transaction.CommitAsync();
        TempData["Success"] = "Payment was recorded and the invoice balance updated.";
        return RedirectToAction(nameof(Index));
    }
}
