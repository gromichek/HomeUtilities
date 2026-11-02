using System.Net;
using System.Text.Json;
using HomeUtilities.Core.Sync;

namespace HomeUtilities.Sync.Google;

/// <summary>Maps failed HTTP calls to <see cref="SyncException"/> kinds. Never puts tokens or codes in a message.</summary>
internal static class GoogleHttp
{
    /// <summary>More than this between our clock and Google's makes tokens look expired or not yet valid.</summary>
    internal static readonly TimeSpan MaxClockSkew = TimeSpan.FromMinutes(5);

    internal static async Task<HttpResponseMessage> SendAsync(HttpClient http, HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            return await http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new SyncException(SyncErrorKind.NoNetwork, "No connection to Google.", ex);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // HttpClient timeout, not the caller's cancellation.
            throw new SyncException(SyncErrorKind.Timeout, "Google did not answer in time.");
        }
    }

    /// <summary>Throws the matching <see cref="SyncException"/> when the response is not a success.</summary>
    internal static async Task EnsureSuccessAsync(HttpResponseMessage response, DateTimeOffset now, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        throw Map(response.StatusCode, response.Headers.Date, body, now);
    }

    internal static SyncException Map(HttpStatusCode status, DateTimeOffset? serverDate, string body, DateTimeOffset now)
    {
        var (error, reason, description) = ParseError(body);

        if (serverDate is { } date && (now - date).Duration() > MaxClockSkew
            && (error == "invalid_grant" || status is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest))
        {
            return new SyncException(SyncErrorKind.ClockSkew, "The clock of this device differs from Google's by more than five minutes.");
        }

        if (error == "invalid_grant" || error == "invalid_token" || status == HttpStatusCode.Unauthorized)
        {
            return new SyncException(SyncErrorKind.TokenRevoked, "The access was revoked or has expired.");
        }

        if (error == "access_denied")
        {
            return new SyncException(SyncErrorKind.ConsentDenied, "The user denied the consent.");
        }

        if (status == HttpStatusCode.NotFound)
        {
            return new SyncException(SyncErrorKind.NotFound, "The file does not exist.");
        }

        if (status == HttpStatusCode.Forbidden && reason is "accessNotConfigured" or "SERVICE_DISABLED" or "serviceDisabled")
        {
            return new SyncException(SyncErrorKind.ApiDisabled, "The Google Drive API is not enabled in the Cloud project.");
        }

        return new SyncException(SyncErrorKind.Other, $"Google answered {(int)status} {error ?? reason}: {description}".TrimEnd(' ', ':'));
    }

    /// <summary>Reads <c>error</c> / <c>error_description</c> (OAuth) or <c>error.errors[0].reason</c> / <c>error.status</c> (Google APIs).</summary>
    private static (string? Error, string? Reason, string? Description) ParseError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return (null, null, null);
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("error", out var error))
            {
                return (null, null, null);
            }

            if (error.ValueKind == JsonValueKind.String)
            {
                return (error.GetString(), null, root.TryGetProperty("error_description", out var d) ? d.GetString() : null);
            }

            string? reason = null;
            if (error.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0
                && errors[0].TryGetProperty("reason", out var r))
            {
                reason = r.GetString();
            }

            if (reason is null && error.TryGetProperty("status", out var s))
            {
                reason = s.GetString();
            }

            // Google also reports a disabled API in error.details[].reason = SERVICE_DISABLED.
            if (error.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Array)
            {
                foreach (var detail in details.EnumerateArray())
                {
                    if (detail.TryGetProperty("reason", out var dr) && dr.GetString() == "SERVICE_DISABLED")
                    {
                        reason = "SERVICE_DISABLED";
                    }
                }
            }

            return (null, reason, error.TryGetProperty("message", out var m) ? m.GetString() : null);
        }
        catch (JsonException)
        {
            return (null, null, null);
        }
    }
}
