using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Web;
using HomeUtilities.Core.Sync;

namespace HomeUtilities.Sync.Google;

/// <summary>Text of the page the browser shows after the sign-in (localized by the caller).</summary>
public sealed record LoopbackPage(string Title, string Message);

/// <summary>
/// A one-shot HTTP listener on <c>http://127.0.0.1:&lt;free port&gt;/</c> that waits for Google's redirect (RFC 8252 §7.3).
/// Disposing it stops the listener, so a cancelled sign-in never leaves the port open.
/// </summary>
public sealed class LoopbackServer : IDisposable
{
    private readonly HttpListener _listener = new();

    private LoopbackServer(int port)
    {
        RedirectUri = $"http://127.0.0.1:{port}/";
        _listener.Prefixes.Add(RedirectUri);
    }

    /// <summary>The redirect URI to register in the authorization request.</summary>
    public string RedirectUri { get; }

    /// <summary>Picks a free port and starts listening.</summary>
    public static LoopbackServer Start()
    {
        // Another process could take the port between the probe and the listener: try a few times.
        for (var attempt = 0; ; attempt++)
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();

            var server = new LoopbackServer(port);
            try
            {
                server._listener.Start();
                return server;
            }
            catch (HttpListenerException) when (attempt < 4)
            {
                server.Dispose();
            }
        }
    }

    /// <summary>
    /// Waits for the request that carries <c>state</c>, <c>code</c> or <c>error</c> (browsers also ask for
    /// <c>/favicon.ico</c>), answers it with <paramref name="page"/> and returns its URI.
    /// </summary>
    public async Task<Uri> WaitForRedirectAsync(LoopbackPage page, CancellationToken ct)
    {
        using var registration = ct.Register(() => _listener.Stop());
        while (true)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                ct.ThrowIfCancellationRequested();
                throw new SyncException(SyncErrorKind.Other, "The sign-in listener stopped.", ex);
            }

            var query = HttpUtility.ParseQueryString(context.Request.Url?.Query ?? string.Empty);
            var isAnswer = query["state"] is not null || query["code"] is not null || query["error"] is not null;
            await RespondAsync(context, isAnswer ? page : null);
            if (isAnswer && context.Request.Url is { } url)
            {
                return url;
            }
        }
    }

    private static async Task RespondAsync(HttpListenerContext context, LoopbackPage? page)
    {
        try
        {
            if (page is null)
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            }
            else
            {
                var html = $"<!doctype html><html><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">"
                    + $"<title>{WebUtility.HtmlEncode(page.Title)}</title></head><body style=\"font-family:sans-serif;margin:3em auto;max-width:30em;padding:0 1em\">"
                    + $"<h2>{WebUtility.HtmlEncode(page.Title)}</h2><p>{WebUtility.HtmlEncode(page.Message)}</p></body></html>";
                var bytes = Encoding.UTF8.GetBytes(html);
                context.Response.ContentType = "text/html; charset=utf-8";
                context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes);
            }
        }
        finally
        {
            context.Response.Close();
        }
    }

    public void Dispose()
    {
        _listener.Close();
    }
}
