using System.Web;
using HomeUtilities.Core.Sync;

namespace HomeUtilities.Sync.Google;

/// <summary>Reads the redirect that Google sends to the loopback address after the user answered the consent page.</summary>
public static class LoopbackRedirect
{
    /// <summary>
    /// Returns the authorization code. Throws <see cref="SyncException"/>: <c>access_denied</c> → ConsentDenied;
    /// a wrong or missing <c>state</c> → Other (a foreign request, not ours); no code → Other.
    /// </summary>
    public static string ParseCode(Uri redirect, string expectedState)
    {
        ArgumentNullException.ThrowIfNull(redirect);
        var query = HttpUtility.ParseQueryString(redirect.Query);

        // The state is checked first: a request without our state is not an answer to our sign-in, whatever else it says.
        if (!string.Equals(query["state"], expectedState, StringComparison.Ordinal))
        {
            throw new SyncException(SyncErrorKind.Other, "The sign-in answer has a wrong state.");
        }

        var error = query["error"];
        if (!string.IsNullOrEmpty(error))
        {
            throw error == "access_denied"
                ? new SyncException(SyncErrorKind.ConsentDenied, "The user denied the consent.")
                : new SyncException(SyncErrorKind.Other, "The sign-in failed: " + error);
        }

        var code = query["code"];
        if (string.IsNullOrEmpty(code))
        {
            throw new SyncException(SyncErrorKind.Other, "The sign-in answer has no code.");
        }

        return code;
    }
}
