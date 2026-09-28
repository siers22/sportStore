using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportStore.Data;
using SportStore.Models;

namespace SportStore.Controllers;

[Authorize(Roles = Roles.Buyer)]
public class CartController(StoreDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var buyerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return View(await db.CartItems.Include(c => c.Product).ThenInclude(p => p.Seller)
            .ThenInclude(s => s.SellerProfile)
            .Where(c => c.BuyerId == buyerId).OrderBy(c => c.Id).ToListAsync());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int productId, int quantity = 1)
    {
        var buyerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var product = await db.Products.FindAsync(productId);
        if (product is null || !product.IsPublished || product.IsHiddenByAdmin || product.Stock < 1) return NotFound();
        quantity = Math.Clamp(quantity, 1, 99);
        var item = await db.CartItems.FirstOrDefaultAsync(c => c.BuyerId == buyerId && c.ProductId == productId);
        if (item is null)
            db.CartItems.Add(new CartItem { BuyerId = buyerId, ProductId = productId, Quantity = Math.Min(quantity, product.Stock) });
        else
            item.Quantity = Math.Min(Math.Min(item.Quantity + quantity, product.Stock), 99);
        await db.SaveChangesAsync();
        TempData["Success"] = "Товар добавлен в корзину.";
        return RedirectToAction("Index");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, int quantity)
    {
        var buyerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var item = await db.CartItems.Include(c => c.Product)
            .FirstOrDefaultAsync(c => c.Id == id && c.BuyerId == buyerId);
        if (item is null) return NotFound();
        if (quantity <= 0 || item.Product.Stock <= 0 || item.Product.IsHiddenByAdmin || !item.Product.IsPublished)
            db.CartItems.Remove(item);
        else item.Quantity = Math.Min(Math.Min(quantity, item.Product.Stock), 99);
        await db.SaveChangesAsync();
        return RedirectToAction("Index");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int id)
    {
        var buyerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var item = await db.CartItems.FirstOrDefaultAsync(c => c.Id == id && c.BuyerId == buyerId);
        if (item is not null)
        {
            db.CartItems.Remove(item);
            await db.SaveChangesAsync();
        }
        return RedirectToAction("Index");
    }
}
