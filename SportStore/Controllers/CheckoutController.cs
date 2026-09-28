using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportStore.Data;
using SportStore.Models;
using SportStore.Services;
using SportStore.ViewModels;

namespace SportStore.Controllers;

[Authorize(Roles = Roles.Buyer)]
public class CheckoutController(StoreDbContext db, CheckoutService checkout) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        await FillViewAsync();
        return View(new CheckoutForm());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(CheckoutForm form)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var id = await checkout.PayAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                    form.RecipientName, form.DeliveryAddress, form.ContactPhone);
                return RedirectToAction("Details", "Orders", new { id });
            }
            catch (CheckoutException error)
            {
                ModelState.AddModelError("", error.Message);
            }
        }
        await FillViewAsync();
        return View(form);
    }

    private async Task FillViewAsync()
    {
        var buyerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        ViewBag.Items = await db.CartItems.Include(c => c.Product)
            .Where(c => c.BuyerId == buyerId).ToListAsync();
        ViewBag.Card = await db.VirtualCards.FirstOrDefaultAsync(c => c.UserId == buyerId);
    }
}
