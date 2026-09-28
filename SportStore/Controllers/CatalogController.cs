using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportStore.Data;

namespace SportStore.Controllers;

public class CatalogController(StoreDbContext db) : Controller
{
    public async Task<IActionResult> Index(string? q, int? categoryId, string? sort)
    {
        var query = db.Products.Include(p => p.Category).Include(p => p.Seller)
            .ThenInclude(s => s.SellerProfile)
            .Where(p => p.IsPublished && !p.IsHiddenByAdmin && p.Stock > 0).AsQueryable();
        if (categoryId.HasValue) query = query.Where(p => p.CategoryId == categoryId.Value);
        var products = await query.ToListAsync();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            products = products.Where(p => p.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                           p.Description.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        products = sort switch
        {
            "price_asc" => products.OrderBy(p => p.Price).ToList(),
            "price_desc" => products.OrderByDescending(p => p.Price).ToList(),
            _ => products.OrderByDescending(p => p.CreatedAt).ToList()
        };
        ViewBag.Categories = await db.Categories.OrderBy(c => c.Name).ToListAsync();
        ViewBag.Query = q;
        ViewBag.CategoryId = categoryId;
        ViewBag.Sort = sort;
        return View(products);
    }

    public async Task<IActionResult> Details(int id)
    {
        var product = await db.Products.Include(p => p.Category).Include(p => p.Seller)
            .ThenInclude(s => s.SellerProfile)
            .FirstOrDefaultAsync(p => p.Id == id && p.IsPublished && !p.IsHiddenByAdmin);
        return product is null ? NotFound() : View(product);
    }
}
