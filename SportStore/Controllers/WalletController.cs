using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportStore.Data;
using SportStore.Models;
using SportStore.ViewModels;
using Microsoft.Data.Sqlite;
using System.Data;

namespace SportStore.Controllers;

[Authorize(Roles = Roles.Buyer)]
public class WalletController(StoreDbContext db) : Controller
{
    private string BuyerId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public async Task<IActionResult> Index()
    {
        var buyerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var card = await db.VirtualCards.SingleOrDefaultAsync(c => c.UserId == buyerId);
        if (card is null) return NotFound();
        ViewBag.Transactions = await db.PaymentTransactions
            .Where(t => t.BuyerId == buyerId).OrderByDescending(t => t.CreatedAt).ToListAsync();
        ViewBag.TopUps = await db.WalletTopUps.Where(t => t.BuyerId == buyerId)
            .OrderByDescending(t => t.CreatedAt).ToListAsync();
        return View(card);
    }

    [HttpGet]
    public async Task<IActionResult> TopUp()
    {
        if (!await FillCardAsync()) return NotFound();
        return View(new TopUpForm());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> TopUp(TopUpForm form)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                var previous = await db.WalletTopUps.FindAsync(form.OperationId);
                if (previous is not null)
                    return previous.BuyerId == BuyerId
                        ? RedirectToAction(nameof(Receipt), new { id = previous.Id }) : BadRequest();

                var card = await db.VirtualCards.SingleOrDefaultAsync(c => c.UserId == BuyerId);
                if (card is null) return NotFound();
                if (card.Balance + form.Amount > 9999999.99m)
                    ModelState.AddModelError(nameof(form.Amount), "После пополнения баланс не должен превышать 9 999 999,99 ₽.");
                else
                {
                    var topUp = new WalletTopUp
                    {
                        Id = form.OperationId, BuyerId = BuyerId, Amount = form.Amount,
                        BalanceBefore = card.Balance, BalanceAfter = card.Balance + form.Amount
                    };
                    card.Balance = topUp.BalanceAfter;
                    db.WalletTopUps.Add(topUp);
                    await db.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return RedirectToAction(nameof(Receipt), new { id = topUp.Id });
                }
            }
            catch (Exception error) when (error is DbUpdateException || error is SqliteException { SqliteErrorCode: 5 or 6 })
            {
                db.ChangeTracker.Clear();
                ModelState.AddModelError("", "Не удалось пополнить карту. Проверьте историю операций и повторите попытку.");
            }
        }
        // Submitted card details are never stored in the database.
        if (!await FillCardAsync()) return NotFound();
        return View(form);
    }

    [HttpGet]
    public async Task<IActionResult> Receipt(Guid id)
    {
        var topUp = await db.WalletTopUps.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id && t.BuyerId == BuyerId);
        if (topUp is null || !await FillCardAsync()) return NotFound();
        return View(topUp);
    }

    private async Task<bool> FillCardAsync()
    {
        ViewBag.Card = await db.VirtualCards.AsNoTracking().SingleOrDefaultAsync(c => c.UserId == BuyerId);
        return ViewBag.Card is not null;
    }
}
