using OrderHub.Core.Ai;

namespace OrderHub.Tests.Fakes;

public class FakeOrderQueryTranslator : IOrderQueryTranslator
{
    private readonly OrderSearchQuery? _result;
    private readonly Exception? _exception;

    public FakeOrderQueryTranslator(OrderSearchQuery? result) => _result = result;

    private FakeOrderQueryTranslator(Exception exception) => _exception = exception;

    public static FakeOrderQueryTranslator Throwing(Exception exception) => new(exception);

    public Task<OrderSearchQuery?> TranslateAsync(string naturalLanguageQuery, CancellationToken cancellationToken = default) =>
        _exception is null ? Task.FromResult(_result) : Task.FromException<OrderSearchQuery?>(_exception);
}
