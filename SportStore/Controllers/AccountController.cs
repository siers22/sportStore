using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SportStore.Data;
using SportStore.Models;
using SportStore.ViewModels;

namespace SportStore.Controllers;

public class AccountController(UserManager<AppUser> users, SignInManager<AppUser> signIn,
    StoreDbContext db) : Controller
{
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View(new LoginForm());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginForm form, string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        if (!ModelState.IsValid) return View(form);
        var login = form.Login.Trim();
        var user = login.Contains('@')
            ? await users.FindByEmailAsync(login)
            : await users.FindByNameAsync(login);
        if (user is null)
        {
            ModelState.AddModelError("", "Не удалось войти. Проверьте данные или статус аккаунта.");
            return View(form);
        }
        if (user.IsBlocked)
        {
            var message = await users.CheckPasswordAsync(user, form.Password)
                ? $"Аккаунт заблокирован. Причина: {user.BlockReason ?? "обратитесь к администратору"}."
                : "Не удалось войти. Проверьте данные или статус аккаунта.";
            ModelState.AddModelError("", message);
            return View(form);
        }
        var result = await signIn.PasswordSignInAsync(user, form.Password, form.RememberMe, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            ModelState.AddModelError("", "Неверный логин или пароль.");
            return View(form);
        }
        return Redirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : Url.Action("Index", "Home")!);
    }

    [HttpGet]
    public IActionResult Register() => View(new RegisterForm());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterForm form)
    {
        if (form.Role is not (Roles.Buyer or Roles.Seller))
            ModelState.AddModelError(nameof(form.Role), "Выберите тип аккаунта.");
        if (form.Role == Roles.Seller && string.IsNullOrWhiteSpace(form.StoreName))
            ModelState.AddModelError(nameof(form.StoreName), "Укажите название магазина.");
        if (!ModelState.IsValid) return View(form);

        var user = new AppUser
        {
            UserName = form.Email.Trim(), Email = form.Email.Trim(),
            DisplayName = form.DisplayName.Trim(), EmailConfirmed = true
        };
        var result = await users.CreateAsync(user, form.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
            return View(form);
        }
        await users.AddToRoleAsync(user, form.Role);
        if (form.Role == Roles.Seller)
            db.SellerProfiles.Add(new SellerProfile { UserId = user.Id, StoreName = form.StoreName!.Trim() });
        else
            db.VirtualCards.Add(new VirtualCard { UserId = user.Id, LastFour = "4242", Balance = 100000m });
        await db.SaveChangesAsync();
        await signIn.SignInAsync(user, isPersistent: false);
        return RedirectToAction("Index", "Home");
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    public IActionResult Denied() => View();
}
