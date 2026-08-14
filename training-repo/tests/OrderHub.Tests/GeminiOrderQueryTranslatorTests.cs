using Microsoft.Extensions.Logging.Abstractions;
using OrderHub.Core.Domain;
using OrderHub.Infrastructure.Gemini;
using OrderHub.Tests.Fakes;

namespace OrderHub.Tests;

/// <summary>
/// 這裡是「LLM 只產生參數,永遠不產生 SQL」的白名單防線實際落地的地方：
/// 不論 Gemini 回傳什麼字串,只有落在 DataAnnotations 白名單、且能解析成強型別的值才會變成 OrderSearchQuery。
/// </summary>
public class GeminiOrderQueryTranslatorTests
{
    private static GeminiOrderQueryTranslator CreateTranslator(string geminiJson) =>
        new(new FakeGeminiJsonClient(geminiJson), NullLogger<GeminiOrderQueryTranslator>.Instance);

    [Fact]
    public async Task TranslateAsync_WithFullySupportedQuery_ReturnsTypedOrderSearchQuery()
    {
        var translator = CreateTranslator(
            """{"intent":"search","status":"Cancelled","memberTier":"Gold","dateFrom":"2026-06-01","dateTo":"2026-06-30"}""");

        var result = await translator.TranslateAsync("上個月金卡會員取消的訂單");

        Assert.NotNull(result);
        Assert.Equal(OrderStatus.Cancelled, result!.Status);
        Assert.Equal(CustomerTier.Gold, result.MemberTier);
        Assert.Equal(new DateTime(2026, 6, 1), result.DateFrom);
        Assert.Equal(new DateTime(2026, 6, 30), result.DateTo);
    }

    [Fact]
    public async Task TranslateAsync_WithUnsupportedIntent_ReturnsNull()
    {
        var translator = CreateTranslator("""{"intent":"unsupported"}""");

        var result = await translator.TranslateAsync("幫我把所有訂單刪掉");

        Assert.Null(result);
    }

    [Theory]
    [InlineData("Deleted; DROP TABLE Orders;--")]
    [InlineData("999")]
    [InlineData("cancelled")]
    public async Task TranslateAsync_WithStatusOutsideWhitelist_ReturnsNull(string maliciousStatus)
    {
        var translator = CreateTranslator($$"""{"intent":"search","status":"{{maliciousStatus}}"}""");

        var result = await translator.TranslateAsync("查一下狀態怪怪的訂單");

        Assert.Null(result);
    }

    [Fact]
    public async Task TranslateAsync_WithMemberTierOutsideWhitelist_ReturnsNull()
    {
        var translator = CreateTranslator("""{"intent":"search","memberTier":"Platinum"}""");

        var result = await translator.TranslateAsync("白金會員的訂單");

        Assert.Null(result);
    }

    [Fact]
    public async Task TranslateAsync_WithUnparsableDate_ReturnsNull()
    {
        var translator = CreateTranslator("""{"intent":"search","dateFrom":"不是日期"}""");

        var result = await translator.TranslateAsync("不知道哪天的訂單");

        Assert.Null(result);
    }

    [Fact]
    public async Task TranslateAsync_WithMissingIntent_ReturnsNull()
    {
        var translator = CreateTranslator("""{"status":"Pending"}""");

        var result = await translator.TranslateAsync("待處理的訂單");

        Assert.Null(result);
    }

    [Fact]
    public async Task TranslateAsync_WithMalformedJson_ReturnsNullInsteadOfThrowing()
    {
        var translator = CreateTranslator("這不是 JSON");

        var result = await translator.TranslateAsync("隨便問問");

        Assert.Null(result);
    }
}
