using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportStore.Data;
using SportStore.Models;

namespace SportStore.Controllers;

[Authorize(Roles = Roles.Buyer)]
public class WalletController(StoreDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var buyerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var card = await db.VirtualCards.SingleOrDefaultAsync(c => c.UserId == buyerId);
        if (card is null) return NotFound();
        ViewBag.Transactions = await db.PaymentTransactions
            .Where(t => t.BuyerId == buyerId).OrderByDescending(t => t.CreatedAt).ToListAsync();
        return View(card);
    }
}
