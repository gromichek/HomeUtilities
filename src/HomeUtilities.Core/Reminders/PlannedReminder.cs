namespace HomeUtilities.Core.Reminders;

/// <summary>A localizable text: a resource key and the arguments for its placeholders.</summary>
public sealed record ReminderText(string Key, IReadOnlyList<object> Args);

/// <summary>
/// Where a tap on a notification leads: a route and its parameters as text, so the target survives being stored in a
/// notification. The view models turn the text into typed navigation parameters.
/// </summary>
public sealed record ReminderTarget(string Route, IReadOnlyDictionary<string, string> Parameters)
{
    /// <summary>The target as <c>Route?key=value&amp;key=value</c> (keys in alphabetical order, values escaped).</summary>
    public string Serialize()
    {
        if (Parameters.Count == 0)
        {
            return Route;
        }

        var parts = Parameters.OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}");
        return $"{Route}?{string.Join('&', parts)}";
    }

    /// <summary>Reads text written by <see cref="Serialize"/>; null if it is empty.</summary>
    public static ReminderTarget? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var split = text.IndexOf('?', StringComparison.Ordinal);
        if (split < 0)
        {
            return new ReminderTarget(text, new Dictionary<string, string>());
        }

        var parameters = new Dictionary<string, string>();
        foreach (var part in text[(split + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var equals = part.IndexOf('=', StringComparison.Ordinal);
            if (equals > 0)
            {
                parameters[Uri.UnescapeDataString(part[..equals])] = Uri.UnescapeDataString(part[(equals + 1)..]);
            }
        }

        return new ReminderTarget(text[..split], parameters);
    }
}

/// <summary>One notification the app wants the system to show.</summary>
/// <param name="Key">Stable identity, e.g. <c>readings:{propertyId}:2026-11-25</c>; the same reminder always has the same key.</param>
/// <param name="At">Local time to show it.</param>
/// <param name="Kind">What it is about.</param>
/// <param name="Title">Title text.</param>
/// <param name="Body">Body text.</param>
/// <param name="Target">The screen a tap opens.</param>
public sealed record PlannedReminder(
    string Key,
    DateTime At,
    ReminderKind Kind,
    ReminderText Title,
    ReminderText Body,
    ReminderTarget Target);
