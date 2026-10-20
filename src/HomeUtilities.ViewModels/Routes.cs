namespace HomeUtilities.ViewModels;

/// <summary>Names of the top-level sections (Shell routes).</summary>
public static class Routes
{
    public const string Overview = "Overview";
    public const string Properties = "Properties";
    public const string Readings = "Readings";
    public const string Bills = "Bills";
    public const string Payments = "Payments";
    public const string Reports = "Reports";
    public const string Settings = "Settings";

    /// <summary>Pushed page (not a section): the property editor, query parameters <c>id</c> or <c>editor</c>.</summary>
    public const string PropertyEdit = "PropertyEdit";
}
