namespace HomeUtilities.ViewModels;

/// <summary>Names of the top-level sections (Shell routes).</summary>
public static class Routes
{
    public const string Overview = "Overview";
    public const string Properties = "Properties";
    public const string Suppliers = "Suppliers";
    public const string Readings = "Readings";
    public const string Bills = "Bills";
    public const string Payments = "Payments";
    public const string Reports = "Reports";
    public const string Settings = "Settings";

    /// <summary>Pushed page (not a section): the property editor, query parameters <c>id</c> or <c>editor</c>.</summary>
    public const string PropertyEdit = "PropertyEdit";

    /// <summary>Pushed page (not a section): the supplier editor, same parameters as <see cref="PropertyEdit"/>.</summary>
    public const string SupplierEdit = "SupplierEdit";

    /// <summary>Pushed page opened from Settings: the list of payment categories.</summary>
    public const string Categories = "Categories";

    /// <summary>Pushed page (not a section): the category editor, same parameters as <see cref="PropertyEdit"/>.</summary>
    public const string CategoryEdit = "CategoryEdit";

    /// <summary>
    /// Pushed page (not a section): the contract editor, parameters <c>editor</c> (a running editor) or <c>propertyId</c>
    /// plus optional <c>id</c>. It is a page in every layout.
    /// </summary>
    public const string ContractEdit = "ContractEdit";

    /// <summary>
    /// Pushed page (not a section): the meter editor, parameters <c>editor</c> (a running editor) or <c>propertyId</c>
    /// plus optional <c>id</c>. It is a page in every layout.
    /// </summary>
    public const string MeterEdit = "MeterEdit";
}
