using System.Globalization;
using Microsoft.Maui.Handlers;

namespace HomeUtilities.App.WinUI;

/// <summary>
/// The calendar pop-up of a <c>DatePicker</c> follows the Windows display language, which is English on a PC whose UI is English
/// even when the app runs in Russian. Sets the native control's <c>Language</c> to the app culture (optional part of T-036c). The text
/// of the field itself is the view model's (UI_GUIDELINES §9); only the pop-up is affected. Not verifiable without a Windows PC —
/// a failure here must never stop the picker from working, so it is swallowed.
/// </summary>
internal static class DatePickerLanguage
{
    public static void Register() =>
        DatePickerHandler.Mapper.AppendToMapping("AppLanguage", (handler, _) =>
        {
            try
            {
                var name = CultureInfo.CurrentUICulture.Name;
                if (name.Length > 0)
                {
                    handler.PlatformView.Language = name;
                }
            }
            catch (ArgumentException)
            {
                // A language tag Windows does not know: keep the system language.
            }
        });
}
