using OrderHub.Core.Domain;

namespace OrderHub.Tests;

public class ProductServiceTests
{
    [Fact]
    public async Task GetAll_ReturnsAllProductsIncludingInactive()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        TestSetup.AddProduct(db, sku: "SKU-A001");
        TestSetup.AddProduct(db, sku: "SKU-A002", isActive: false);

        var products = await service.GetAllAsync();

        Assert.Equal(2, products.Count);
    }

    [Fact]
    public async Task GetActive_ExcludesInactiveProducts()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        TestSetup.AddProduct(db, sku: "SKU-A001");
        TestSetup.AddProduct(db, sku: "SKU-A002", isActive: false);

        var products = await service.GetActiveAsync();

        Assert.All(products, p => Assert.True(p.IsActive));
        Assert.Single(products);
    }

    [Fact]
    public async Task GetLowStock_FiltersByThresholdAndSortsByStockAscending()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        var low = TestSetup.AddProduct(db, sku: "SKU-LOW1", stock: 1);
        var mid = TestSetup.AddProduct(db, sku: "SKU-LOW3", stock: 3);
        TestSetup.AddProduct(db, sku: "SKU-EQ10", stock: 10);
        TestSetup.AddProduct(db, sku: "SKU-HIGH20", stock: 20);

        var result = await service.GetLowStockAsync(10);

        Assert.Equal(2, result.Count);
        Assert.Equal(low.Id, result[0].Product.Id);
        Assert.Equal(mid.Id, result[1].Product.Id);
    }

    [Fact]
    public async Task GetLowStock_ExcludesInactiveProducts()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        var active = TestSetup.AddProduct(db, sku: "SKU-ACTIVE", stock: 3);
        TestSetup.AddProduct(db, sku: "SKU-INACTIVE", stock: 3, isActive: false);

        var result = await service.GetLowStockAsync(10);

        Assert.Single(result);
        Assert.Equal(active.Id, result[0].Product.Id);
    }

    [Fact]
    public async Task GetLowStock_QuantitySoldLast30Days_ExcludesCancelledAndOldOrders()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        var customer = TestSetup.AddCustomer(db);
        var product = TestSetup.AddProduct(db, stock: 3);

        db.Orders.AddRange(
            new Order
            {
                CustomerId = customer.Id,
                Status = OrderStatus.Confirmed,
                CreatedAt = DateTime.UtcNow.AddDays(-30).AddHours(1), // 剛好在 30 天窗口內側（差 1 小時，避免測試執行耗時造成誤判）
                Items = { new OrderItem { ProductId = product.Id, Quantity = 5, UnitPriceSnapshot = 100m } }
            },
            new Order
            {
                CustomerId = customer.Id,
                Status = OrderStatus.Cancelled,
                CreatedAt = DateTime.UtcNow.AddDays(-29),
                Items = { new OrderItem { ProductId = product.Id, Quantity = 100, UnitPriceSnapshot = 100m } }
            },
            new Order
            {
                CustomerId = customer.Id,
                Status = OrderStatus.Confirmed,
                CreatedAt = DateTime.UtcNow.AddDays(-30).AddHours(-1), // 剛好在 30 天窗口外側
                Items = { new OrderItem { ProductId = product.Id, Quantity = 100, UnitPriceSnapshot = 100m } }
            });
        db.SaveChanges();

        var result = await service.GetLowStockAsync(10);

        Assert.Equal(5, result.Single(r => r.Product.Id == product.Id).QuantitySoldLast30Days);
    }
}
