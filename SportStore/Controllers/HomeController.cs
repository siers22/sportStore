using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportStore.Data;

namespace SportStore.Controllers;

public class HomeController(StoreDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        ViewBag.Categories = await db.Categories.OrderBy(c => c.Name).ToListAsync();
        return View(await db.Products.Include(p => p.Category).Include(p => p.Seller)
            .ThenInclude(s => s.SellerProfile)
            .Where(p => !p.IsDeleted && p.IsPublished && !p.IsHiddenByAdmin && p.Stock > 0)
            .OrderByDescending(p => p.CreatedAt).Take(8).ToListAsync());
    }

    public IActionResult Error() => View();
}
