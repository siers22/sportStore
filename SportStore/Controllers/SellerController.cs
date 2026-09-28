using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportStore.Data;
using SportStore.Models;
using SportStore.ViewModels;

namespace SportStore.Controllers;

[Authorize(Roles = Roles.Seller)]
public class SellerController(StoreDbContext db) : Controller
{
    private string SellerId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public async Task<IActionResult> Index()
    {
        ViewBag.Store = await db.SellerProfiles.FirstOrDefaultAsync(s => s.UserId == SellerId);
        ViewBag.ProductCount = await db.Products.CountAsync(p => p.SellerId == SellerId);
        ViewBag.OrderCount = await db.SellerOrders.CountAsync(o => o.SellerId == SellerId);
        ViewBag.Revenue = (await db.SellerOrders.Where(o => o.SellerId == SellerId && o.Status != OrderStatus.Cancelled)
            .Select(o => o.Total).ToListAsync()).Sum();
        return View(await db.SellerOrders.Include(o => o.Purchase)
            .Where(o => o.SellerId == SellerId).OrderByDescending(o => o.Purchase.CreatedAt)
            .Take(5).ToListAsync());
    }

    public async Task<IActionResult> Products() => View(await db.Products.Include(p => p.Category)
        .Where(p => p.SellerId == SellerId).OrderByDescending(p => p.CreatedAt).ToListAsync());

    [HttpGet]
    public async Task<IActionResult> CreateProduct()
    {
        await FillCategoriesAsync();
        return View("ProductForm", new ProductForm());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateProduct(ProductForm form)
    {
        if (!await db.Categories.AnyAsync(c => c.Id == form.CategoryId))
            ModelState.AddModelError(nameof(form.CategoryId), "Выберите категорию.");
        if (!ModelState.IsValid)
        {
            await FillCategoriesAsync();
            return View("ProductForm", form);
        }
        db.Products.Add(new Product
        {
            SellerId = SellerId, Name = form.Name.Trim(), Description = form.Description.Trim(),
            Price = form.Price, Stock = form.Stock, CategoryId = form.CategoryId,
            Visual = CleanVisual(form.Visual), IsPublished = form.IsPublished
        });
        await db.SaveChangesAsync();
        TempData["Success"] = "Товар добавлен.";
        return RedirectToAction("Products");
    }

    [HttpGet]
    public async Task<IActionResult> EditProduct(int id)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id && p.SellerId == SellerId);
        if (product is null) return NotFound();
        await FillCategoriesAsync();
        ViewBag.ProductId = id;
        ViewBag.ModerationStatus = product.ModerationStatus;
        ViewBag.ModerationReason = product.ModerationReason;
        return View("ProductForm", new ProductForm
        {
            Name = product.Name, Description = product.Description, Price = product.Price,
            Stock = product.Stock, CategoryId = product.CategoryId, Visual = product.Visual,
            IsPublished = product.IsPublished
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditProduct(int id, ProductForm form)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id && p.SellerId == SellerId);
        if (product is null) return NotFound();
        if (!await db.Categories.AnyAsync(c => c.Id == form.CategoryId))
            ModelState.AddModelError(nameof(form.CategoryId), "Выберите категорию.");
        if (!ModelState.IsValid)
        {
            await FillCategoriesAsync();
            ViewBag.ProductId = id;
            ViewBag.ModerationStatus = product.ModerationStatus;
            ViewBag.ModerationReason = product.ModerationReason;
            return View("ProductForm", form);
        }
        product.Name = form.Name.Trim();
        product.Description = form.Description.Trim();
        product.Price = form.Price;
        product.Stock = form.Stock;
        product.CategoryId = form.CategoryId;
        product.Visual = CleanVisual(form.Visual);
        product.IsPublished = form.IsPublished;
        if (product.IsHiddenByAdmin)
            product.ModerationStatus = ProductModerationStatus.PendingReview;
        await db.SaveChangesAsync();
        TempData["Success"] = product.IsHiddenByAdmin
            ? "Изменения сохранены. Товар отправлен администратору на проверку."
            : "Товар обновлён.";
        return RedirectToAction("Products");
    }

    public async Task<IActionResult> Orders() => View(await db.SellerOrders
        .Include(o => o.Purchase).ThenInclude(p => p.Buyer)
        .Include(o => o.Items)
        .Where(o => o.SellerId == SellerId).OrderByDescending(o => o.Purchase.CreatedAt).ToListAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AdvanceOrder(int id)
    {
        var order = await db.SellerOrders.FirstOrDefaultAsync(o => o.Id == id && o.SellerId == SellerId);
        if (order is null) return NotFound();
        order.Status = order.Status switch
        {
            OrderStatus.Paid => OrderStatus.Processing,
            OrderStatus.Processing => OrderStatus.Shipped,
            OrderStatus.Shipped => OrderStatus.Completed,
            _ => order.Status
        };
        await db.SaveChangesAsync();
        return RedirectToAction("Orders");
    }

    private async Task FillCategoriesAsync() => ViewBag.Categories =
        await db.Categories.OrderBy(c => c.Name).ToListAsync();

    private static string CleanVisual(string? value) => value is
        "shoe" or "backpack" or "rope" or "dumbbell" or "mat" or "football" or
        "basketball" or "tent" or "bottle" or "lamp" or "helmet" ? value : "default";
}
