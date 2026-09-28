using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportStore.Data;
using SportStore.Models;

namespace SportStore.Controllers;

[Authorize(Roles = Roles.Admin)]
public class AdminController(StoreDbContext db, UserManager<AppUser> users) : Controller
{
    public async Task<IActionResult> Index()
    {
        ViewBag.UserCount = await db.Users.CountAsync();
        ViewBag.ProductCount = await db.Products.CountAsync();
        ViewBag.OrderCount = await db.SellerOrders.CountAsync();
        ViewBag.OpenComplaints = await db.Complaints.CountAsync(c => c.Status != ComplaintStatus.Closed);
        return View(await db.Complaints.Include(c => c.Product).Include(c => c.Buyer)
            .OrderByDescending(c => c.CreatedAt).Take(5).ToListAsync());
    }

    public async Task<IActionResult> Users()
    {
        var all = await db.Users.OrderByDescending(u => u.CreatedAt).ToListAsync();
        var roleMap = new Dictionary<string, string>();
        foreach (var user in all)
            roleMap[user.Id] = string.Join(", ", await users.GetRolesAsync(user));
        ViewBag.Roles = roleMap;
        return View(all);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> BlockUser(string id, string? reason)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null) return NotFound();
        if (await users.IsInRoleAsync(user, Roles.Admin)) return BadRequest("Нельзя заблокировать администратора.");
        if (!ValidReason(reason))
        {
            TempData["Error"] = "Укажите причину блокировки (до 1000 символов).";
            return RedirectToAction("Users");
        }
        user.IsBlocked = true;
        user.BlockReason = reason!.Trim();
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded) return BadRequest(string.Join("; ", result.Errors.Select(e => e.Description)));
        await users.UpdateSecurityStampAsync(user);
        TempData["Success"] = "Аккаунт заблокирован.";
        return RedirectToAction("Users");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UnblockUser(string id)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null) return NotFound();
        if (await users.IsInRoleAsync(user, Roles.Admin)) return BadRequest();
        user.IsBlocked = false;
        user.BlockReason = null;
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded) return BadRequest(string.Join("; ", result.Errors.Select(e => e.Description)));
        await users.UpdateSecurityStampAsync(user);
        TempData["Success"] = "Аккаунт разблокирован.";
        return RedirectToAction("Users");
    }

    public async Task<IActionResult> Products() => View(await db.Products.Include(p => p.Category)
        .Include(p => p.Seller).ThenInclude(s => s.SellerProfile)
        .OrderByDescending(p => p.CreatedAt).ToListAsync());

    public async Task<IActionResult> Orders() => View(await db.SellerOrders
        .Include(o => o.Purchase).ThenInclude(p => p.Buyer)
        .Include(o => o.Seller).ThenInclude(s => s.SellerProfile)
        .Include(o => o.Items)
        .OrderByDescending(o => o.Purchase.CreatedAt).ToListAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> HideProduct(int id, string? reason)
    {
        var product = await db.Products.FindAsync(id);
        if (product is null) return NotFound();
        if (!ValidReason(reason))
        {
            TempData["Error"] = "Укажите причину скрытия товара (до 1000 символов).";
            return RedirectToAction("Products");
        }
        product.IsHiddenByAdmin = true;
        product.ModerationStatus = ProductModerationStatus.ChangesRequested;
        product.ModerationReason = reason!.Trim();
        await db.SaveChangesAsync();
        TempData["Success"] = "Товар скрыт. Продавец увидит причину и сможет отправить исправления на проверку.";
        return RedirectToAction("Products");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveProduct(int id)
    {
        var product = await db.Products.FindAsync(id);
        if (product is null) return NotFound();
        if (product.ModerationStatus != ProductModerationStatus.PendingReview) return BadRequest("Товар ещё не отправлен на проверку.");
        product.IsHiddenByAdmin = false;
        product.ModerationStatus = ProductModerationStatus.Approved;
        product.ModerationReason = null;
        await db.SaveChangesAsync();
        TempData["Success"] = product.IsPublished
            ? "Товар одобрен и возвращён в каталог."
            : "Товар одобрен. Для появления в каталоге продавцу нужно включить публикацию.";
        return RedirectToAction("Products");
    }

    public async Task<IActionResult> Complaints() => View(await db.Complaints
        .Include(c => c.Product).Include(c => c.Buyer)
        .OrderBy(c => c.Status == ComplaintStatus.Closed)
        .ThenByDescending(c => c.CreatedAt).ToListAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewComplaint(int id, ComplaintStatus status, string? comment, bool hideProduct)
    {
        var complaint = await db.Complaints.Include(c => c.Product).FirstOrDefaultAsync(c => c.Id == id);
        if (complaint is null) return NotFound();
        if (status is not (ComplaintStatus.InReview or ComplaintStatus.Closed)) return BadRequest();
        if (comment?.Length > 1000) return BadRequest("Комментарий слишком длинный.");
        complaint.Status = status;
        complaint.AdminComment = comment?.Trim();
        if (status == ComplaintStatus.Closed)
        {
            complaint.ResolvedAt = DateTime.UtcNow;
            complaint.ResolvedById = User.FindFirstValue(ClaimTypes.NameIdentifier);
        }
        else
        {
            complaint.ResolvedAt = null;
            complaint.ResolvedById = null;
        }
        if (hideProduct)
        {
            if (!ValidReason(comment))
            {
                TempData["Error"] = "Для скрытия товара укажите причину в комментарии (до 1000 символов).";
                return RedirectToAction("Complaints");
            }
            complaint.Product.IsHiddenByAdmin = true;
            complaint.Product.ModerationStatus = ProductModerationStatus.ChangesRequested;
            complaint.Product.ModerationReason = comment!.Trim();
        }
        await db.SaveChangesAsync();
        TempData["Success"] = "Жалоба обновлена.";
        return RedirectToAction("Complaints");
    }

    private static bool ValidReason(string? reason) => !string.IsNullOrWhiteSpace(reason) && reason.Trim().Length <= 1000;
}
