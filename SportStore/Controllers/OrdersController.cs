using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportStore.Data;
using SportStore.Models;

namespace SportStore.Controllers;

[Authorize(Roles = Roles.Buyer)]
public class OrdersController(StoreDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var buyerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return View(await db.Purchases.Include(p => p.SellerOrders)
            .Where(p => p.BuyerId == buyerId).OrderByDescending(p => p.CreatedAt).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var buyerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var purchase = await db.Purchases.Include(p => p.SellerOrders)
            .ThenInclude(o => o.Items)
            .Include(p => p.SellerOrders).ThenInclude(o => o.Seller)
            .ThenInclude(s => s.SellerProfile)
            .FirstOrDefaultAsync(p => p.Id == id && p.BuyerId == buyerId);
        return purchase is null ? NotFound() : View(purchase);
    }
}
