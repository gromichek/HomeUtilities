using System.Globalization;
using HomeUtilities.Core;
using HomeUtilities.Core.Reminders;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>
/// Opens the screen a tapped notification points to. The notification carries the serialized
/// <see cref="ReminderTarget"/>; this turns it into the typed navigation parameters of the app. Unknown or damaged data
/// opens the Overview, never throws.
/// </summary>
public sealed class NotificationTapHandler(INavigationService navigation, BillFilterState billFilter, IClock clock)
{
    /// <summary>Opens the target in <paramref name="data"/>.</summary>
    public Task OpenAsync(string? data)
    {
        var target = ReminderTarget.Parse(data);
        if (target is null)
        {
            return navigation.GoToAsync(Routes.Overview);
        }

        var parameters = target.Parameters;
        switch (target.Route)
        {
            case ReminderRoutes.ReadingsFor when Guid(parameters, ReminderRoutes.PropertyIdParameter) is { } propertyId
                && Date(parameters, ReminderRoutes.DateParameter) is { } date:
                return navigation.GoToAsync(
                    Routes.ReadingsFor,
                    new Dictionary<string, object>
                    {
                        [EditorNavigation.PropertyIdParameter] = propertyId,
                        [ReadingsViewModel.DateParameter] = date,
                    });

            case ReminderRoutes.BillEdit when Guid(parameters, ReminderRoutes.IdParameter) is { } billId:
                return navigation.GoToAsync(Routes.BillEdit, new Dictionary<string, object> { [EditorNavigation.IdParameter] = billId });

            case ReminderRoutes.MeterEdit when Guid(parameters, ReminderRoutes.IdParameter) is { } meterId
                && Guid(parameters, ReminderRoutes.PropertyIdParameter) is { } meterPropertyId:
                return navigation.GoToAsync(
                    Routes.MeterEdit,
                    new Dictionary<string, object>
                    {
                        [EditorNavigation.IdParameter] = meterId,
                        [EditorNavigation.PropertyIdParameter] = meterPropertyId,
                    });

            case ReminderRoutes.Bills:
                billFilter.PropertyId = Guid(parameters, ReminderRoutes.PropertyIdParameter);
                billFilter.CategoryId = null;
                billFilter.Period = PeriodFilter.ForAllYears(clock.Today.Year);
                billFilter.Status = parameters.TryGetValue(ReminderRoutes.StatusParameter, out var status)
                    && Enum.TryParse<BillStatusFilter>(status, out var filter)
                        ? filter
                        : BillStatusFilter.All;
                return navigation.GoToAsync(Routes.Bills);

            default:
                return navigation.GoToAsync(Routes.Overview);
        }
    }

    private static Guid? Guid(IReadOnlyDictionary<string, string> parameters, string key) =>
        parameters.TryGetValue(key, out var text) && System.Guid.TryParse(text, out var id) ? id : null;

    private static DateOnly? Date(IReadOnlyDictionary<string, string> parameters, string key) =>
        parameters.TryGetValue(key, out var text) && DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
}
