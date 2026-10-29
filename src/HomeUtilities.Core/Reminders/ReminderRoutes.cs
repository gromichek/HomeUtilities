namespace HomeUtilities.Core.Reminders;

/// <summary>
/// The routes and parameter names a <see cref="ReminderTarget"/> uses. They are the same text as the navigation routes of
/// the view models (a test keeps them equal); Core cannot reference the view models.
/// </summary>
public static class ReminderRoutes
{
    /// <summary>The Overview section (target of a summary).</summary>
    public const string Overview = "Overview";

    /// <summary>The readings screen of a property at a date: <c>propertyId</c>, <c>date</c> (yyyy-MM-dd).</summary>
    public const string ReadingsFor = "ReadingsFor";

    /// <summary>The bill editor: <c>id</c>.</summary>
    public const string BillEdit = "BillEdit";

    /// <summary>The meter editor: <c>id</c>, <c>propertyId</c>.</summary>
    public const string MeterEdit = "MeterEdit";

    /// <summary>The Bills section filtered by <c>status</c> (a status filter name), optionally by <c>propertyId</c>.</summary>
    public const string Bills = "Bills";

    /// <summary>Parameter: the id of the bill or meter.</summary>
    public const string IdParameter = "id";

    /// <summary>Parameter: the property id.</summary>
    public const string PropertyIdParameter = "propertyId";

    /// <summary>Parameter: a date as yyyy-MM-dd.</summary>
    public const string DateParameter = "date";

    /// <summary>Parameter: the bill status filter name.</summary>
    public const string StatusParameter = "status";
}
