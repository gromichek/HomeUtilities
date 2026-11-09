using Android.Graphics.Drawables;
using Android.Widget;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;

namespace HomeUtilities.App;

/// <summary>
/// The bordered field look on Android (UI_GUIDELINES §6, R-07): text fields get a rounded outline in the field-outline colour (the
/// accent while focused) instead of the platform's underline — which was black in the dark theme (owner's screenshot 2026-11-05).
/// Choice and date fields draw the same box themselves (<c>ChoiceField</c>, <c>DateField</c>). Windows text boxes already draw a
/// bordered box and are left alone.
/// </summary>
internal static class FieldBorder
{
    private static readonly List<WeakReference<EditText>> Tracked = [];
    private static bool _themeHooked;

    /// <summary>Registers the look for every <c>Entry</c> and <c>Editor</c>; call once while the app is built.</summary>
    public static void Register()
    {
        EntryHandler.Mapper.AppendToMapping("FieldBorder", (handler, _) => Track(handler.PlatformView));
        EditorHandler.Mapper.AppendToMapping("FieldBorder", (handler, _) => Track(handler.PlatformView));

        // Number fields (Keyboard="Numeric") on Android, configured after every MAUI mapping that rewrites the input settings:
        // - no full-screen "extract" keyboard in landscape (T-042a): ExtractEditText dropped the decimal separator
        //   ("360,34" became "36034"); text fields keep the big full-screen keyboard (owner 2026-11-09);
        // - both "." and "," accepted (owner 2026-11-09, tablet): MAUI's number key listener allows only the separator of the app
        //   culture (",") while the Samsung tablet keyboard offers only "." — nothing decimal could be typed. DecimalInput parses
        //   both, as agreed at the start of the project;
        // - the app culture as the keyboard's hint locale, so keyboards that can offer "," do so.
        foreach (var key in new[] { "NumberField", nameof(IEntry.Keyboard), nameof(IEntry.ReturnType), nameof(IEntry.IsReadOnly),
                     nameof(IEntry.IsTextPredictionEnabled), nameof(IEntry.IsSpellCheckEnabled), ImeOptionsKey })
        {
            EntryHandler.Mapper.AppendToMapping(key, (handler, _) => ConfigureNumberField(handler));
        }

        // Last line of defence, whatever mapping ran last: when a number field gets the focus with other settings, configure it
        // again and restart the input connection.
        EntryHandler.Mapper.AppendToMapping("NumberFieldOnFocus", (handler, _) => GuardOnFocus(handler));

        // The default background mapping runs again when a view's background changes and would bring the underline back.
        EntryHandler.Mapper.AppendToMapping(nameof(IView.Background), (handler, _) => Apply(handler.PlatformView));
        EditorHandler.Mapper.AppendToMapping(nameof(IView.Background), (handler, _) => Apply(handler.PlatformView));
    }

    private static readonly string ImeOptionsKey =
        Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.Entry.ImeOptionsProperty.PropertyName;

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<EditText, object> FocusGuarded = [];

    private static readonly Android.Text.Method.DigitsKeyListener NumberKeys =
        Android.Text.Method.DigitsKeyListener.GetInstance("0123456789.,-")!;

    private const Android.Text.InputTypes NumberInput =
        Android.Text.InputTypes.ClassNumber | Android.Text.InputTypes.NumberFlagDecimal | Android.Text.InputTypes.NumberFlagSigned;

    private static bool IsNumber(EditText edit) =>
        (edit.InputType & Android.Text.InputTypes.MaskClass) == Android.Text.InputTypes.ClassNumber;

    private static bool IsConfigured(EditText edit) =>
        ReferenceEquals(edit.KeyListener, NumberKeys)
        && ((int)edit.ImeOptions & (int)Android.Views.InputMethods.ImeFlags.NoExtractUi) != 0;

    private static void ConfigureNumberField(IEntryHandler handler)
    {
        var edit = handler.PlatformView;

        // Read-only fields are left alone: setting a key listener makes a view focusable again.
        if (edit is null || !IsNumber(edit) || handler.VirtualView is null || handler.VirtualView.IsReadOnly)
        {
            return;
        }

        edit.ImeOptions = (Android.Views.InputMethods.ImeAction)((int)edit.ImeOptions | (int)Android.Views.InputMethods.ImeFlags.NoExtractUi);

        if (!ReferenceEquals(edit.KeyListener, NumberKeys))
        {
            // SetKeyListener switches the input type to the listener's (text, as "," is not a standard number key);
            // SetRawInputType restores the number keyboard without replacing the listener.
            edit.KeyListener = NumberKeys;
            edit.SetRawInputType(NumberInput);
        }

        var culture = System.Globalization.CultureInfo.CurrentCulture.Name;
        if (culture.Length > 0)
        {
            edit.ImeHintLocales = new Android.OS.LocaleList(Java.Util.Locale.ForLanguageTag(culture)!);
        }
    }

    private static void GuardOnFocus(IEntryHandler handler)
    {
        var edit = handler.PlatformView;
        if (edit is null || !FocusGuarded.TryAdd(edit, new object()))
        {
            return;
        }

        var weakHandler = new WeakReference<IEntryHandler>(handler);
        edit.FocusChange += (sender, args) =>
        {
            if (!args.HasFocus || sender is not EditText focused || !IsNumber(focused) || IsConfigured(focused)
                || !weakHandler.TryGetTarget(out var current) || !ReferenceEquals(current.PlatformView, focused))
            {
                return;
            }

            ConfigureNumberField(current);
            var input = focused.Context?.GetSystemService(Android.Content.Context.InputMethodService)
                as Android.Views.InputMethods.InputMethodManager;
            input?.RestartInput(focused);
        };
    }

    private static void Track(EditText edit)
    {
        lock (Tracked)
        {
            Tracked.RemoveAll(reference => !reference.TryGetTarget(out _));
            Tracked.Add(new WeakReference<EditText>(edit));
        }

        if (!_themeHooked && Microsoft.Maui.Controls.Application.Current is { } app)
        {
            _themeHooked = true;
            app.RequestedThemeChanged += (_, _) => ReapplyAll();
        }

        Apply(edit);
    }

    private static void ReapplyAll()
    {
        List<EditText> alive;
        lock (Tracked)
        {
            alive = [.. Tracked.Select(reference => reference.TryGetTarget(out var edit) ? edit : null).OfType<EditText>()];
        }

        foreach (var edit in alive)
        {
            Apply(edit);
        }
    }

    private static void Apply(EditText edit)
    {
        var app = Microsoft.Maui.Controls.Application.Current;
        var dark = app?.RequestedTheme == AppTheme.Dark;
        if (app?.Resources is not { } resources
            || edit.Context is not { } context
            || !resources.TryGetValue(dark ? "FieldOutlineDark" : "FieldOutlineLight", out var outlineValue) || outlineValue is not Color outline
            || !resources.TryGetValue(dark ? "PrimaryDark" : "PrimaryLight", out var accentValue) || accentValue is not Color accent)
        {
            return;
        }

        var radius = (float)context.ToPixels(8);
        var normal = Box(radius, (int)context.ToPixels(1), outline);
        var focused = Box(radius, (int)context.ToPixels(2), accent);
        var states = new StateListDrawable();
        states.AddState([global::Android.Resource.Attribute.StateFocused], focused);
        states.AddState([], normal);
        edit.Background = states;

        // Text starts inside the box, 12 dp from the edge like the other fields.
        var horizontal = (int)context.ToPixels(12);
        var vertical = (int)context.ToPixels(10);
        edit.SetPadding(horizontal, vertical, horizontal, vertical);
    }

    private static GradientDrawable Box(float radius, int stroke, Color color)
    {
        var box = new GradientDrawable();
        box.SetShape(ShapeType.Rectangle);
        box.SetCornerRadius(radius);
        box.SetStroke(stroke, color.ToPlatform());
        box.SetColor(global::Android.Graphics.Color.Transparent);
        return box;
    }
}
