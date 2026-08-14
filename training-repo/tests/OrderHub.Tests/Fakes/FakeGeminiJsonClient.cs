using OrderHub.Infrastructure.Gemini;

namespace OrderHub.Tests.Fakes;

public class FakeGeminiJsonClient : IGeminiJsonClient
{
    private readonly string? _json;
    private readonly Exception? _exception;

    public FakeGeminiJsonClient(string json) => _json = json;

    private FakeGeminiJsonClient(Exception exception) => _exception = exception;

    public static FakeGeminiJsonClient Throwing(Exception exception) => new(exception);

    public Task<string> GenerateJsonAsync(string input, string responseSchemaJson, CancellationToken cancellationToken = default) =>
        _exception is null ? Task.FromResult(_json!) : Task.FromException<string>(_exception);
}
