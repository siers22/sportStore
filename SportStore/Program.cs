using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SportStore.Data;
using SportStore.Models;
using SportStore.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
var dataDirectory = Environment.GetEnvironmentVariable("SPORTSTORE_DATA_DIR") ?? AppContext.BaseDirectory;
Directory.CreateDirectory(dataDirectory);
var databasePath = Path.Combine(dataDirectory, "sportstore.db");
builder.Services.AddDbContext<StoreDbContext>(options =>
    options.UseSqlite($"Data Source={databasePath}"));
builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.User.RequireUniqueEmail = true;
})
    .AddEntityFrameworkStores<StoreDbContext>()
    .AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/Denied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
});
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    options.ValidationInterval = TimeSpan.Zero);
builder.Services.AddScoped<CheckoutService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

using (var scope = app.Services.CreateScope())
{
    await DbSeeder.SeedAsync(scope.ServiceProvider);
}

app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.Run();
