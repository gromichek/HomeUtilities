namespace HomeUtilities.ViewModels;

/// <summary>Reads files bundled with the app package (read-only), by their logical path such as <c>guide/en.md</c>.</summary>
public interface IAppAssetReader
{
    /// <summary>Opens the asset; throws <see cref="FileNotFoundException"/> when it is not part of the package.</summary>
    Task<Stream> OpenReadAsync(string path);
}
