namespace HomeUtilities.ViewModels;

public interface IThemeService
{
    AppThemeChoice Current { get; }

    /// <summary>Applies the theme and persists the choice.</summary>
    void Set(AppThemeChoice theme);
}
