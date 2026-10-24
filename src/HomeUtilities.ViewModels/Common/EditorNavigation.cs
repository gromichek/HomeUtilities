namespace HomeUtilities.ViewModels.Common;

/// <summary>Names of the navigation parameters shared by all list + editor screens.</summary>
public static class EditorNavigation
{
    /// <summary>Carries a running editor instance to the editor page (so entered data survives a layout switch).</summary>
    public const string EditorParameter = "editor";

    /// <summary>Carries the id of the item to load into a fresh editor; absent means a new item.</summary>
    public const string IdParameter = "id";

    /// <summary>Carries the id of the property a new contract belongs to.</summary>
    public const string PropertyIdParameter = "propertyId";

    /// <summary>Carries the id of the contract a new tariff component belongs to.</summary>
    public const string ContractIdParameter = "contractId";
}
