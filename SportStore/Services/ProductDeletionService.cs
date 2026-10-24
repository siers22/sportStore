using System.Data;
using Microsoft.EntityFrameworkCore;
using SportStore.Data;

namespace SportStore.Services;

public class ProductDeletionService(StoreDbContext db, ProductImageStore images, ILogger<ProductDeletionService> logger)
{
    // Keep the product row so existing orders and complaints retain their references.
    // The seller ID restricts the operation to that seller; administrators omit it.
    public async Task<bool> DeleteAsync(int productId, string? sellerId = null)
    {
        string? imagePath;
        await using (var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
        {
            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted &&
                (sellerId == null || p.SellerId == sellerId));
            if (product is null) return false;

            imagePath = product.ImagePath;
            product.IsDeleted = true;
            product.IsPublished = false;
            product.ImagePath = null;
            await db.CartItems.Where(item => item.ProductId == productId).ExecuteDeleteAsync();
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        try { images.Delete(imagePath); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(error, "Could not remove the image for deleted product {ProductId}", productId);
        }
        return true;
    }
}
