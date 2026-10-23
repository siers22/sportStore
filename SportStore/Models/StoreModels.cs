using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace SportStore.Models;

public static class Roles
{
    public const string Buyer = "Buyer";
    public const string Seller = "Seller";
    public const string Admin = "Admin";
}

public class AppUser : IdentityUser
{
    [Required, StringLength(80)]
    public string DisplayName { get; set; } = "";
    public bool IsBlocked { get; set; }
    [StringLength(1000)]
    public string? BlockReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public SellerProfile? SellerProfile { get; set; }
    public VirtualCard? VirtualCard { get; set; }
}

public class SellerProfile
{
    public int Id { get; set; }
    [Required, StringLength(100)]
    public string StoreName { get; set; } = "";
    [StringLength(500)]
    public string Description { get; set; } = "";
    public string UserId { get; set; } = "";
    public AppUser User { get; set; } = null!;
}

public class Category
{
    public int Id { get; set; }
    [Required, StringLength(80)]
    public string Name { get; set; } = "";
    [Required, StringLength(80)]
    public string Slug { get; set; } = "";
}

public class Product
{
    public int Id { get; set; }
    [Required, StringLength(140)]
    public string Name { get; set; } = "";
    [Required, StringLength(2000)]
    public string Description { get; set; } = "";
    [Range(0.01, 9999999)]
    public decimal Price { get; set; }
    [Range(0, 1000000)]
    public int Stock { get; set; }
    public bool IsPublished { get; set; } = true;
    public bool IsHiddenByAdmin { get; set; }
    public ProductModerationStatus ModerationStatus { get; set; } = ProductModerationStatus.Approved;
    [StringLength(1000)]
    public string? ModerationReason { get; set; }
    [StringLength(30)]
    public string Visual { get; set; } = "default";
    [StringLength(200)]
    public string? ImagePath { get; set; }
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public string SellerId { get; set; } = "";
    public AppUser Seller { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum ProductModerationStatus { Approved, ChangesRequested, PendingReview }

public class CartItem
{
    public int Id { get; set; }
    public string BuyerId { get; set; } = "";
    public AppUser Buyer { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    [Range(1, 99)]
    public int Quantity { get; set; }
}

public class VirtualCard
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public AppUser User { get; set; } = null!;
    [StringLength(4, MinimumLength = 4)]
    public string LastFour { get; set; } = "4242";
    public decimal Balance { get; set; } = 100000m;
}

public class Purchase
{
    public int Id { get; set; }
    public string BuyerId { get; set; } = "";
    public AppUser Buyer { get; set; } = null!;
    [Required, StringLength(100)]
    public string RecipientName { get; set; } = "";
    [Required, StringLength(200)]
    public string DeliveryAddress { get; set; } = "";
    [Required, StringLength(30)]
    public string ContactPhone { get; set; } = "";
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<SellerOrder> SellerOrders { get; set; } = [];
}

public enum OrderStatus { Paid, Processing, Shipped, Completed, Cancelled }

public class SellerOrder
{
    public int Id { get; set; }
    public int PurchaseId { get; set; }
    public Purchase Purchase { get; set; } = null!;
    public string SellerId { get; set; } = "";
    public AppUser Seller { get; set; } = null!;
    public OrderStatus Status { get; set; } = OrderStatus.Paid;
    public decimal Total { get; set; }
    public List<OrderItem> Items { get; set; } = [];
}

public class OrderItem
{
    public int Id { get; set; }
    public int SellerOrderId { get; set; }
    public SellerOrder SellerOrder { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    [Required, StringLength(140)]
    public string ProductName { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}

public class PaymentTransaction
{
    public int Id { get; set; }
    public int PurchaseId { get; set; }
    public Purchase Purchase { get; set; } = null!;
    public string BuyerId { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class WalletTopUp
{
    public Guid Id { get; set; }
    public string BuyerId { get; set; } = "";
    public AppUser Buyer { get; set; } = null!;
    public decimal Amount { get; set; }
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum ComplaintStatus { New, InReview, Closed }

public class Complaint
{
    public int Id { get; set; }
    public string BuyerId { get; set; } = "";
    public AppUser Buyer { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    [Required, StringLength(1000)]
    public string Reason { get; set; } = "";
    public ComplaintStatus Status { get; set; } = ComplaintStatus.New;
    [StringLength(1000)]
    public string? AdminComment { get; set; }
    public string? ResolvedById { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
