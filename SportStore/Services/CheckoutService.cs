using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using SportStore.Data;
using SportStore.Models;

namespace SportStore.Services;

public class CheckoutException : Exception
{
    public CheckoutException(string message) : base(message) { }
}

public class CheckoutService(StoreDbContext db)
{
    public async Task<int> PayAsync(string buyerId, string recipient, string address, string phone)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        var cart = await db.CartItems.Include(c => c.Product)
            .Where(c => c.BuyerId == buyerId).ToListAsync();
        if (cart.Count == 0) throw new CheckoutException("Корзина пуста.");

        var card = await db.VirtualCards.SingleOrDefaultAsync(c => c.UserId == buyerId)
            ?? throw new CheckoutException("Виртуальная карта не найдена.");
        foreach (var item in cart)
        {
            if (!item.Product.IsPublished || item.Product.IsHiddenByAdmin || item.Product.Stock < item.Quantity)
                throw new CheckoutException($"Товара «{item.Product.Name}» больше нет в нужном количестве.");
        }

        var total = cart.Sum(c => c.Product.Price * c.Quantity);
        if (card.Balance < total) throw new CheckoutException("На виртуальной карте недостаточно средств.");

        var purchase = new Purchase
        {
            BuyerId = buyerId,
            RecipientName = recipient.Trim(),
            DeliveryAddress = address.Trim(),
            ContactPhone = phone.Trim(),
            Total = total
        };
        foreach (var group in cart.GroupBy(c => c.Product.SellerId))
        {
            var order = new SellerOrder
            {
                SellerId = group.Key,
                Total = group.Sum(c => c.Product.Price * c.Quantity),
                Items = group.Select(c => new OrderItem
                {
                    ProductId = c.ProductId,
                    ProductName = c.Product.Name,
                    UnitPrice = c.Product.Price,
                    Quantity = c.Quantity
                }).ToList()
            };
            purchase.SellerOrders.Add(order);
        }

        var before = card.Balance;
        card.Balance -= total;
        foreach (var item in cart) item.Product.Stock -= item.Quantity;
        db.Purchases.Add(purchase);
        db.PaymentTransactions.Add(new PaymentTransaction
        {
            Purchase = purchase, BuyerId = buyerId, Amount = total,
            BalanceBefore = before, BalanceAfter = card.Balance
        });
        db.CartItems.RemoveRange(cart);

        try
        {
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            throw new CheckoutException("Не удалось завершить оплату. Обновите корзину и попробуйте ещё раз.");
        }
        catch (SqliteException error) when (error.SqliteErrorCode is 5 or 6)
        {
            throw new CheckoutException("База занята. Подождите немного и повторите оплату.");
        }
        return purchase.Id;
    }
}
