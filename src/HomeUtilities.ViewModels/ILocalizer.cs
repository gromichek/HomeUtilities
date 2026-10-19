namespace HomeUtilities.ViewModels;

/// <summary>Resolves UI strings by resource key in the current language.</summary>
public interface ILocalizer
{
    string this[string key] { get; }

    string Get(string key, params object[] args);
}
