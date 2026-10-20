namespace HomeUtilities.ViewModels;

/// <summary>Resolves UI strings by resource key in the current language.</summary>
public interface ILocalizer
{
    string this[string key] { get; }

    string Get(string key, params object[] args);

    /// <summary>Looks up a key without a placeholder fallback; false if the key has no resource.</summary>
    bool TryGet(string key, out string value);
}
