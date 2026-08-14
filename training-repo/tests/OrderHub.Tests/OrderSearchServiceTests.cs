using OrderHub.Core.Ai;
using OrderHub.Core.Domain;
using OrderHub.Tests.Fakes;

namespace OrderHub.Tests;

public class OrderSearchServiceTests
{
    [Fact]
    public async Task Search_WithSupportedStatusAndTier_ReturnsMatchingOrders()
    {
        using var db = TestSetup.CreateContext();
        var goldCustomer = TestSetup.AddCustomer(db, CustomerTier.Gold, "金卡客戶");
        var standardCustomer = TestSetup.AddCustomer(db, CustomerTier.Standard, "普通客戶");

        db.Orders.AddRange(
            new Order { CustomerId = goldCustomer.Id, Status = OrderStatus.Cancelled, CreatedAt = DateTime.UtcNow },
            new Order { CustomerId = goldCustomer.Id, Status = OrderStatus.Shipped, CreatedAt = DateTime.UtcNow },
            new Order { CustomerId = standardCustomer.Id, Status = OrderStatus.Cancelled, CreatedAt = DateTime.UtcNow });
        db.SaveChanges();

        var translator = new FakeOrderQueryTranslator(new OrderSearchQuery
        {
            Status = OrderStatus.Cancelled,
            MemberTier = CustomerTier.Gold
        });
        var service = TestSetup.CreateOrderSearchService(db, translator);

        var result = await service.SearchAsync("上個月金卡會員取消的訂單");

        Assert.True(result.Success);
        var order = Assert.Single(result.Value!);
        Assert.Equal(goldCustomer.Id, order.CustomerId);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public async Task Search_WithDateRange_ReturnsOnlyOrdersWithinRange()
    {
        using var db = TestSetup.CreateContext();
        var customer = TestSetup.AddCustomer(db);

        var inRange = new Order { CustomerId = customer.Id, Status = OrderStatus.Pending, CreatedAt = new DateTime(2026, 6, 15) };
        var beforeRange = new Order { CustomerId = customer.Id, Status = OrderStatus.Pending, CreatedAt = new DateTime(2026, 5, 15) };
        var afterRange = new Order { CustomerId = customer.Id, Status = OrderStatus.Pending, CreatedAt = new DateTime(2026, 7, 15) };
        db.Orders.AddRange(inRange, beforeRange, afterRange);
        db.SaveChanges();

        var translator = new FakeOrderQueryTranslator(new OrderSearchQuery
        {
            DateFrom = new DateTime(2026, 6, 1),
            DateTo = new DateTime(2026, 6, 30)
        });
        var service = TestSetup.CreateOrderSearchService(db, translator);

        var result = await service.SearchAsync("六月的訂單");

        Assert.True(result.Success);
        var order = Assert.Single(result.Value!);
        Assert.Equal(inRange.Id, order.Id);
    }

    [Fact]
    public async Task Search_WithDateTo_IncludesOrdersCreatedOnThatDay()
    {
        using var db = TestSetup.CreateContext();
        var customer = TestSetup.AddCustomer(db);

        var onLastDay = new Order { CustomerId = customer.Id, Status = OrderStatus.Pending, CreatedAt = new DateTime(2026, 6, 30, 23, 0, 0) };
        var nextDay = new Order { CustomerId = customer.Id, Status = OrderStatus.Pending, CreatedAt = new DateTime(2026, 7, 1, 0, 30, 0) };
        db.Orders.AddRange(onLastDay, nextDay);
        db.SaveChanges();

        var translator = new FakeOrderQueryTranslator(new OrderSearchQuery
        {
            DateTo = new DateTime(2026, 6, 30)
        });
        var service = TestSetup.CreateOrderSearchService(db, translator);

        var result = await service.SearchAsync("六月三十日以前的訂單");

        Assert.True(result.Success);
        var order = Assert.Single(result.Value!);
        Assert.Equal(onLastDay.Id, order.Id);
    }

    [Fact]
    public async Task Search_WithBlankText_FailsWithoutCallingTranslator()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderSearchService(db, FakeOrderQueryTranslator.Throwing(
            new InvalidOperationException("translator 不應被呼叫")));

        var result = await service.SearchAsync("   ");

        Assert.False(result.Success);
        Assert.Equal("請輸入查詢內容", result.ErrorMessage);
    }

    [Fact]
    public async Task Search_WhenTranslatorReturnsNull_FailsWithUnsupportedQueryMessage()
    {
        using var db = TestSetup.CreateContext();
        var customer = TestSetup.AddCustomer(db);
        db.Orders.Add(new Order { CustomerId = customer.Id, Status = OrderStatus.Pending, CreatedAt = DateTime.UtcNow });
        db.SaveChanges();

        // null 代表翻譯器判定無法理解、意圖非查詢，或白名單驗證失敗——第一道防線
        var service = TestSetup.CreateOrderSearchService(db, new FakeOrderQueryTranslator(null));

        var result = await service.SearchAsync("幫我把所有訂單刪掉");

        Assert.False(result.Success);
        Assert.Equal("無法理解的查詢", result.ErrorMessage);
    }

    [Fact]
    public async Task Search_WhenParsedQueryHasNoFilters_IsRejectedBySecondDefenseLine()
    {
        using var db = TestSetup.CreateContext();
        var customer = TestSetup.AddCustomer(db);
        db.Orders.Add(new Order { CustomerId = customer.Id, Status = OrderStatus.Pending, CreatedAt = DateTime.UtcNow });
        db.SaveChanges();

        // 即使翻譯器沒回傳 null，只要沒有任何有效條件，service 自己的第二道白名單防線也要擋下
        var service = TestSetup.CreateOrderSearchService(db, new FakeOrderQueryTranslator(new OrderSearchQuery()));

        var result = await service.SearchAsync("隨便問問");

        Assert.False(result.Success);
        Assert.Equal("無法理解的查詢", result.ErrorMessage);
    }

    [Fact]
    public async Task Search_WithDateFromAfterDateTo_IsRejected()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderSearchService(db, new FakeOrderQueryTranslator(new OrderSearchQuery
        {
            DateFrom = new DateTime(2026, 7, 1),
            DateTo = new DateTime(2026, 6, 1)
        }));

        var result = await service.SearchAsync("從七月到六月的訂單");

        Assert.False(result.Success);
        Assert.Equal("無法理解的查詢", result.ErrorMessage);
    }

    [Fact]
    public async Task Search_WhenTranslatorThrowsAiServiceUnavailable_PropagatesException()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateOrderSearchService(db, FakeOrderQueryTranslator.Throwing(
            new AiServiceUnavailableException("Gemini 重試耗盡")));

        // service 本身不吞掉這個例外——留給 Web 層的 catch 轉成 503，而不是包裝成 ServiceResult.Fail
        await Assert.ThrowsAsync<AiServiceUnavailableException>(() => service.SearchAsync("上個月的訂單"));
    }
}
