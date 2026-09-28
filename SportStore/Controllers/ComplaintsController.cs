using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportStore.Data;
using SportStore.Models;
using SportStore.ViewModels;

namespace SportStore.Controllers;

[Authorize(Roles = Roles.Buyer)]
public class ComplaintsController(StoreDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var buyerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return View(await db.Complaints.Include(c => c.Product)
            .Where(c => c.BuyerId == buyerId).OrderByDescending(c => c.CreatedAt).ToListAsync());
    }

    [HttpGet]
    public async Task<IActionResult> Create(int productId)
    {
        ViewBag.Product = await db.Products.FindAsync(productId);
        if (ViewBag.Product is null) return NotFound();
        return View(new ComplaintForm { ProductId = productId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ComplaintForm form)
    {
        var product = await db.Products.FindAsync(form.ProductId);
        if (product is null) return NotFound();
        if (!ModelState.IsValid)
        {
            ViewBag.Product = product;
            return View(form);
        }
        db.Complaints.Add(new Complaint
        {
            BuyerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!,
            ProductId = form.ProductId, Reason = form.Reason.Trim()
        });
        await db.SaveChangesAsync();
        TempData["Success"] = "Жалоба отправлена администратору.";
        return RedirectToAction("Index");
    }
}
