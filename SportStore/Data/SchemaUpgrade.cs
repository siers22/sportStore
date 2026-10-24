using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace SportStore.Data;

// Compatibility upgrade for databases created by the original EnsureCreated version.
public static class SchemaUpgrade
{
    public static async Task ApplyAsync(StoreDbContext db)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = "PRAGMA table_info('Products')";
        var columns = new HashSet<string>();
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                columns.Add(reader.GetString(1));
        }
        if (!columns.Contains("ImagePath"))
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE Products ADD COLUMN ImagePath TEXT NULL");
        if (!columns.Contains("IsDeleted"))
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE Products ADD COLUMN IsDeleted INTEGER NOT NULL DEFAULT 0");
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS WalletTopUps (
                Id TEXT NOT NULL CONSTRAINT PK_WalletTopUps PRIMARY KEY,
                BuyerId TEXT NOT NULL,
                Amount TEXT NOT NULL,
                BalanceBefore TEXT NOT NULL,
                BalanceAfter TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                CONSTRAINT FK_WalletTopUps_AspNetUsers_BuyerId
                    FOREIGN KEY (BuyerId) REFERENCES AspNetUsers (Id) ON DELETE RESTRICT
            );
            CREATE INDEX IF NOT EXISTS IX_WalletTopUps_BuyerId ON WalletTopUps (BuyerId);
            """);
        await transaction.CommitAsync();
    }
}
