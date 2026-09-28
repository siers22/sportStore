using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SportStore.Models;

namespace SportStore.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<StoreDbContext>();
        await db.Database.EnsureCreatedAsync();
        await UpdateSchemaAsync(db);

        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { Roles.Buyer, Roles.Seller, Roles.Admin })
        {
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole(role));
        }

        var users = services.GetRequiredService<UserManager<AppUser>>();
        await CreateUserAsync(users, "admin@sport.local", "Администратор", "Admin123!", Roles.Admin);
        var seller1 = await CreateUserAsync(users, "trail@sport.local", "Алексей", "Demo123!", Roles.Seller);
        var seller2 = await CreateUserAsync(users, "arena@sport.local", "Мария", "Demo123!", Roles.Seller);
        var buyer = await CreateUserAsync(users, "buyer@sport.local", "Дмитрий", "Demo123!", Roles.Buyer);

        if (!await db.SellerProfiles.AnyAsync())
        {
            db.SellerProfiles.AddRange(
                new SellerProfile { UserId = seller1.Id, StoreName = "Тропа", Description = "Снаряжение для движения и приключений." },
                new SellerProfile { UserId = seller2.Id, StoreName = "Арена", Description = "Инвентарь для тренировок каждый день." });
        }

        if (!await db.VirtualCards.AnyAsync(c => c.UserId == buyer.Id))
            db.VirtualCards.Add(new VirtualCard { UserId = buyer.Id, LastFour = "4242", Balance = 100000m });

        if (!await db.Categories.AnyAsync())
        {
            db.Categories.AddRange(
                new Category { Name = "Бег", Slug = "running" },
                new Category { Name = "Фитнес", Slug = "fitness" },
                new Category { Name = "Командные игры", Slug = "team" },
                new Category { Name = "Туризм", Slug = "outdoor" },
                new Category { Name = "Велоспорт", Slug = "cycling" });
        }
        await db.SaveChangesAsync();

        if (!await db.Products.AnyAsync())
        {
            var categories = await db.Categories.ToDictionaryAsync(c => c.Slug, c => c.Id);
            db.Products.AddRange(
                MakeProduct("Кроссовки Pulse Run", "Лёгкая пара для ежедневных пробежек. Дышащий верх и мягкая амортизация.", 8490, 16, "running", "shoe", seller1.Id),
                MakeProduct("Беговой рюкзак Vector", "Компактный рюкзак для длительных маршрутов. Удобная посадка и отделения для воды.", 5290, 12, "running", "backpack", seller1.Id),
                MakeProduct("Скакалка Tempo", "Регулируемая скоростная скакалка для разминки и интервальных тренировок.", 1290, 38, "fitness", "rope", seller2.Id),
                MakeProduct("Гантель Atlas 10 кг", "Разборная гантель с надёжным хватом для домашних силовых занятий.", 3690, 22, "fitness", "dumbbell", seller2.Id),
                MakeProduct("Коврик Balance", "Нескользящий коврик толщиной 6 мм для йоги, растяжки и упражнений.", 2390, 31, "fitness", "mat", seller2.Id),
                MakeProduct("Футбольный мяч Match Pro", "Прочный тренировочный мяч стандартного размера для поля и манежа.", 2890, 27, "team", "football", seller2.Id),
                MakeProduct("Баскетбольный мяч Street 7", "Цепкая поверхность для игры на открытой площадке.", 3190, 18, "team", "basketball", seller2.Id),
                MakeProduct("Палатка North 2", "Двухместная палатка с лёгким каркасом и защитой от дождя.", 12990, 9, "outdoor", "tent", seller1.Id),
                MakeProduct("Термос Summit 750", "Стальной термос для горячих и холодных напитков в поездке.", 2290, 24, "outdoor", "bottle", seller1.Id),
                MakeProduct("Фонарь Trail Light", "Яркий налобный фонарь с несколькими режимами свечения.", 1790, 42, "outdoor", "lamp", seller1.Id),
                MakeProduct("Велошлем Aero", "Лёгкий шлем с вентиляцией и удобной регулировкой размера.", 4590, 15, "cycling", "helmet", seller1.Id),
                MakeProduct("Велобутылка Flow", "Удобная бутылка объёмом 650 мл для тренировок и поездок.", 890, 55, "cycling", "bottle", seller1.Id));

            await db.SaveChangesAsync();

            Product MakeProduct(string name, string description, decimal price, int stock, string category,
                string visual, string sellerId) => new()
                {
                    Name = name, Description = description, Price = price, Stock = stock,
                    CategoryId = categories[category], Visual = visual, SellerId = sellerId
                };
        }
    }

    // EnsureCreated does not add columns to databases created by earlier versions.
    private static async Task UpdateSchemaAsync(StoreDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();
        try
        {
            await AddColumnIfMissingAsync("AspNetUsers", "BlockReason", "TEXT NULL");
            await AddColumnIfMissingAsync("Products", "ModerationStatus", "TEXT NOT NULL DEFAULT 'Approved'");
            await AddColumnIfMissingAsync("Products", "ModerationReason", "TEXT NULL");
            await db.Database.ExecuteSqlRawAsync("UPDATE Products SET ModerationStatus = 'ChangesRequested' WHERE IsHiddenByAdmin = 1 AND ModerationStatus = 'Approved'");
        }
        finally
        {
            await connection.CloseAsync();
        }

        async Task AddColumnIfMissingAsync(string table, string column, string definition)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info(\"{table}\")";
            await using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                    if (reader.GetString(1) == column) return;
            }

            await using var alter = connection.CreateCommand();
            alter.CommandText = $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {definition}";
            await alter.ExecuteNonQueryAsync();
        }
    }

    private static async Task<AppUser> CreateUserAsync(UserManager<AppUser> manager, string email,
        string name, string password, string role)
    {
        var user = await manager.FindByEmailAsync(email);
        if (user is not null) return user;

        user = new AppUser { UserName = email, Email = email, DisplayName = name, EmailConfirmed = true };
        var result = await manager.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        await manager.AddToRoleAsync(user, role);
        return user;
    }
}
