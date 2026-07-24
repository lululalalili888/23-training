using Microsoft.EntityFrameworkCore;
using OrderHub.Core.Common;
using OrderHub.Core.Domain;
using OrderHub.Core.Interfaces;
using OrderHub.Infrastructure.Data;

namespace OrderHub.Infrastructure.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly OrderHubDbContext _db;

    public ProductRepository(OrderHubDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync() =>
        await _db.Products.OrderBy(p => p.Sku).ToListAsync();

    public async Task<IReadOnlyList<Product>> GetActiveAsync() =>
        await _db.Products.Where(p => p.IsActive).OrderBy(p => p.Sku).ToListAsync();

    public Task<Product?> GetByIdAsync(int id) =>
        _db.Products.FirstOrDefaultAsync(p => p.Id == id);

    public async Task<IReadOnlyList<LowStockProductInfo>> GetLowStockAsync(int threshold)
    {
        var lowStockProducts = await _db.Products
            .Where(p => p.IsActive && p.StockQuantity < threshold)
            .OrderBy(p => p.StockQuantity)
            .ToListAsync();

        var productIds = lowStockProducts.Select(p => p.Id).ToList();
        var cutoff = DateTime.UtcNow.AddDays(-30);

        var soldQuantities = await _db.OrderItems
            .Where(i => productIds.Contains(i.ProductId)
                && i.Order!.CreatedAt >= cutoff
                && i.Order.Status != OrderStatus.Cancelled)
            .GroupBy(i => i.ProductId)
            .Select(g => new { ProductId = g.Key, Quantity = g.Sum(i => i.Quantity) })
            .ToDictionaryAsync(x => x.ProductId, x => x.Quantity);

        return lowStockProducts
            .Select(p => new LowStockProductInfo(p, soldQuantities.TryGetValue(p.Id, out var quantity) ? quantity : 0))
            .ToList();
    }

    public Task SaveChangesAsync() => _db.SaveChangesAsync();
}
