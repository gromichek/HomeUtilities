namespace HomeUtilities.ViewModels;

/// <summary>Names of the top-level sections (Shell routes).</summary>
public static class Routes
{
    public const string Overview = "Overview";
    public const string Properties = "Properties";
    public const string Suppliers = "Suppliers";
    public const string Readings = "Readings";
    public const string Charges = "Charges";
    public const string Bills = "Bills";
    public const string Payments = "Payments";
    public const string Reports = "Reports";
    public const string Settings = "Settings";

    /// <summary>The user guide as a menu section (opened at the top).</summary>
    public const string Help = "Help";

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

    /// <summary>
    /// Pushed page (not a section): the reading history of one meter, parameters <c>editor</c> (a loaded
    /// <see cref="ReadingHistoryViewModel"/>) or <c>id</c> (the meter id).
    /// </summary>
    public const string ReadingHistory = "ReadingHistory";

    /// <summary>
    /// Pushed page (not a section): the user guide scrolled to a section, parameter <c>topic</c> (a help topic name).
    /// Back returns to the screen that opened it.
    /// </summary>
    public const string HelpSection = "HelpSection";

    /// <summary>
    /// Pushed page (not a section): the tariff component editor, parameters <c>contractId</c> plus optional <c>id</c>.
    /// It is a page in every layout.
    /// </summary>
    public const string TariffComponentEdit = "TariffComponentEdit";

    /// <summary>
    /// Pushed page (not a section): the charges screen opened for a contract ("Show charges"), parameters <c>propertyId</c>
    /// and optional <c>contractId</c>. Back returns to the caller.
    /// </summary>
    public const string ChargesFor = "ChargesFor";

    /// <summary>
    /// Pushed page (not a section): the readings screen opened from a charge warning, parameters <c>propertyId</c> and
    /// <c>date</c> (a <see cref="DateOnly"/>). Back returns to the charges.
    /// </summary>
    public const string ReadingsFor = "ReadingsFor";

    /// <summary>Pushed page opened from Settings: the VAT catalog.</summary>
    public const string VatRates = "VatRates";

    /// <summary>Pushed page (not a section): the VAT rate editor, same parameters as <see cref="PropertyEdit"/>.</summary>
    public const string VatRateEdit = "VatRateEdit";

    /// <summary>
    /// Pushed page (not a section): the bill editor, parameters <c>editor</c> (a running editor) or <c>id</c> (absent = a new
    /// bill), and for a new bill optionally <c>contractId</c>, <c>propertyId</c>, <c>period</c> (<c>yyyy-MM</c>) and <c>gross</c>
    /// (the calculated total, shown as a hint).
    /// </summary>
    public const string BillEdit = "BillEdit";

    /// <summary>Pushed page opened from Settings: the list of bill templates.</summary>
    public const string BillTemplates = "BillTemplates";

    /// <summary>
    /// Pushed page (not a section): the bill template editor, same parameters as <see cref="PropertyEdit"/>; a running editor
    /// is handed over by <see cref="BillTemplateLauncher"/> ("Template from this contract", "Save as template").
    /// </summary>
    public const string BillTemplateEdit = "BillTemplateEdit";

    /// <summary>
    /// Pushed page (not a section): the bills screen opened for a property ("All bills" of a contract), parameters
    /// <c>propertyId</c> and optional <c>contractId</c> (highlighted). Back returns to the caller.
    /// </summary>
    public const string BillsFor = "BillsFor";

    /// <summary>
    /// Pushed page (not a section): the payment editor, parameters <c>editor</c> (a running editor) or <c>id</c> (absent = a new
    /// payment), and for a new payment optionally <c>billId</c>, <c>contractId</c> or <c>propertyId</c>.
    /// </summary>
    public const string PaymentEdit = "PaymentEdit";

    /// <summary>
    /// Pushed page (not a section): the payments screen opened for a property and a contract ("All payments" in the contract
    /// editor), parameters <c>propertyId</c> and <c>contractId</c>. Back returns to the caller.
    /// </summary>
    public const string PaymentsFor = "PaymentsFor";

    /// <summary>
    /// Pushed page (not a section): the settlement of an equalized contract (month table and the expected result), parameter
    /// <c>contractId</c>. Back returns to the contract editor.
    /// </summary>
    public const string EqualizedSettlement = "EqualizedSettlement";

    /// <summary>Pushed page opened from Settings: the Excel import wizard.</summary>
    public const string ImportWizard = "ImportWizard";

    /// <summary>Pushed page opened from Settings or the wizard: the list of imports with Undo and the saved import profiles.</summary>
    public const string Imports = "Imports";
}
