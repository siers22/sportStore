using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SportStore.Models;

namespace SportStore.Data;

public class StoreDbContext(DbContextOptions<StoreDbContext> options) : IdentityDbContext<AppUser>(options)
{
    public DbSet<SellerProfile> SellerProfiles => Set<SellerProfile>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<VirtualCard> VirtualCards => Set<VirtualCard>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<SellerOrder> SellerOrders => Set<SellerOrder>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<WalletTopUp> WalletTopUps => Set<WalletTopUp>();
    public DbSet<Complaint> Complaints => Set<Complaint>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AppUser>().HasOne(u => u.SellerProfile).WithOne(p => p.User)
            .HasForeignKey<SellerProfile>(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<AppUser>().HasOne(u => u.VirtualCard).WithOne(c => c.User)
            .HasForeignKey<VirtualCard>(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Category>().HasIndex(c => c.Slug).IsUnique();
        builder.Entity<CartItem>().HasIndex(c => new { c.BuyerId, c.ProductId }).IsUnique();
        builder.Entity<PaymentTransaction>().HasIndex(t => t.PurchaseId).IsUnique();
        builder.Entity<Product>().Property(p => p.Price).HasPrecision(12, 2);
        builder.Entity<VirtualCard>().Property(c => c.Balance).HasPrecision(12, 2);
        builder.Entity<Purchase>().Property(p => p.Total).HasPrecision(12, 2);
        builder.Entity<SellerOrder>().Property(o => o.Total).HasPrecision(12, 2);
        builder.Entity<OrderItem>().Property(i => i.UnitPrice).HasPrecision(12, 2);
        builder.Entity<PaymentTransaction>().Property(t => t.Amount).HasPrecision(12, 2);
        builder.Entity<PaymentTransaction>().Property(t => t.BalanceBefore).HasPrecision(12, 2);
        builder.Entity<PaymentTransaction>().Property(t => t.BalanceAfter).HasPrecision(12, 2);
        builder.Entity<WalletTopUp>().Property(t => t.Amount).HasPrecision(12, 2);
        builder.Entity<WalletTopUp>().Property(t => t.BalanceBefore).HasPrecision(12, 2);
        builder.Entity<WalletTopUp>().Property(t => t.BalanceAfter).HasPrecision(12, 2);
        builder.Entity<WalletTopUp>().HasIndex(t => t.BuyerId);
        builder.Entity<WalletTopUp>().HasOne(t => t.Buyer).WithMany()
            .HasForeignKey(t => t.BuyerId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<SellerOrder>().Property(o => o.Status).HasConversion<string>();
        builder.Entity<Complaint>().Property(c => c.Status).HasConversion<string>();
        builder.Entity<Product>().Property(p => p.ModerationStatus).HasConversion<string>();

        builder.Entity<Product>().HasOne(p => p.Seller).WithMany().HasForeignKey(p => p.SellerId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<CartItem>().HasOne(c => c.Buyer).WithMany().HasForeignKey(c => c.BuyerId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Purchase>().HasOne(p => p.Buyer).WithMany().HasForeignKey(p => p.BuyerId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<SellerOrder>().HasOne(o => o.Seller).WithMany().HasForeignKey(o => o.SellerId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<OrderItem>().HasOne(i => i.Product).WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Complaint>().HasOne(c => c.Buyer).WithMany().HasForeignKey(c => c.BuyerId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<PaymentTransaction>().HasOne(t => t.Purchase).WithMany().HasForeignKey(t => t.PurchaseId).OnDelete(DeleteBehavior.Cascade);
    }
}
