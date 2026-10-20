namespace HomeUtilities.ViewModels;

/// <summary>Sent through the weak messenger after the UI language was changed.</summary>
public sealed record LanguageChangedMessage(string Language);
