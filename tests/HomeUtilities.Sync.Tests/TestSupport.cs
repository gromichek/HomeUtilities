using System.Net;
using System.Text;
using HomeUtilities.Core.Sync;

namespace HomeUtilities.Sync.Tests;

/// <summary>A request as the fake handler saw it (body already read).</summary>
public sealed record RecordedRequest(HttpMethod Method, Uri Uri, string? Authorization, string Body, string? ContentType);

/// <summary>Answers requests with prepared responses and records them; a thrown exception simulates a network failure.</summary>
public sealed class FakeHandler : HttpMessageHandler
{
    private readonly Queue<Func<RecordedRequest, HttpResponseMessage>> _responses = new();

    public List<RecordedRequest> Requests { get; } = [];

    public FakeHandler Enqueue(HttpStatusCode status, string body, DateTimeOffset? date = null)
    {
        _responses.Enqueue(_ =>
        {
            var response = new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            response.Headers.Date = date;
            return response;
        });
        return this;
    }

    public FakeHandler EnqueueBytes(byte[] bytes)
    {
        _responses.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
        return this;
    }

    public FakeHandler EnqueueFailure(Exception exception)
    {
        _responses.Enqueue(_ => throw exception);
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        var recorded = new RecordedRequest(
            request.Method, request.RequestUri!, request.Headers.Authorization?.ToString(), body, request.Content?.Headers.ContentType?.ToString());
        Requests.Add(recorded);
        Assert.True(_responses.Count > 0, $"Unexpected request {request.Method} {request.RequestUri}");
        return _responses.Dequeue()(recorded);
    }
}

public sealed class FixedTime(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

public sealed class MemorySecretStore : ISecretStore
{
    public Dictionary<string, string> Values { get; } = [];

    public Task<string?> GetAsync(string key) => Task.FromResult(Values.TryGetValue(key, out var value) ? value : null);

    public Task SetAsync(string key, string value)
    {
        Values[key] = value;
        return Task.CompletedTask;
    }

    public void Remove(string key) => Values.Remove(key);
}

public static class Tokens
{
    /// <summary>An unsigned ID token whose payload has the e-mail claim.</summary>
    public static string IdToken(string email)
    {
        static string B64(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{B64("{\"alg\":\"none\"}")}.{B64($"{{\"email\":\"{email}\"}}")}.sig";
    }

    public static string TokenAnswer(string access = "access-1", string? refresh = "refresh-1", string? email = "anna@example.test", int expiresIn = 3600)
    {
        var parts = new List<string> { $"\"access_token\":\"{access}\"", $"\"expires_in\":{expiresIn}", "\"token_type\":\"Bearer\"" };
        if (refresh is not null)
        {
            parts.Add($"\"refresh_token\":\"{refresh}\"");
        }

        if (email is not null)
        {
            parts.Add($"\"id_token\":\"{IdToken(email)}\"");
        }

        return "{" + string.Join(",", parts) + "}";
    }
}
