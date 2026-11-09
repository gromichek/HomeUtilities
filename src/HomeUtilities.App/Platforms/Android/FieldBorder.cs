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

        // Landscape: no full-screen "extract" keyboard (T-042a). ExtractEditText does not keep the field's number key listener, so
        // the decimal separator was dropped there ("360,34" became "36034"); with NoExtractUi the field stays visible and edited itself.
        EntryHandler.Mapper.AppendToMapping("NoExtractUi", (handler, _) => NoExtractUi(handler.PlatformView));
        EditorHandler.Mapper.AppendToMapping("NoExtractUi", (handler, _) => NoExtractUi(handler.PlatformView));

        // The default background mapping runs again when a view's background changes and would bring the underline back.
        EntryHandler.Mapper.AppendToMapping(nameof(IView.Background), (handler, _) => Apply(handler.PlatformView));
        EditorHandler.Mapper.AppendToMapping(nameof(IView.Background), (handler, _) => Apply(handler.PlatformView));
    }

    private static void NoExtractUi(EditText edit) => edit.ImeOptions |= Android.Views.InputMethods.ImeFlags.NoExtractUi;

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
