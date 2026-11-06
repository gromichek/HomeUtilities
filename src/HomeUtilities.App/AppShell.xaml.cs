using HomeUtilities.App.Views;
using HomeUtilities.ViewModels;

namespace HomeUtilities.App;

public partial class AppShell : Shell
{
    private readonly INavigationGuard _guard;
    private bool _bypassGuard;

    public AppShell(ShellViewModel viewModel, INavigationGuard guard)
    {
        _guard = guard;
        InitializeComponent();
        Routing.RegisterRoute(Routes.PropertyEdit, typeof(PropertyEditPage));
        Routing.RegisterRoute(Routes.SupplierEdit, typeof(SupplierEditPage));
        Routing.RegisterRoute(Routes.ContractEdit, typeof(ContractEditPage));
        Routing.RegisterRoute(Routes.MeterEdit, typeof(MeterEditPage));
        Routing.RegisterRoute(Routes.ReadingHistory, typeof(ReadingHistoryPage));
        Routing.RegisterRoute(Routes.HelpSection, typeof(HelpPage));
        Routing.RegisterRoute(Routes.Categories, typeof(CategoriesPage));
        Routing.RegisterRoute(Routes.CategoryEdit, typeof(CategoryEditPage));
        Routing.RegisterRoute(Routes.TariffComponentEdit, typeof(TariffComponentEditPage));
        Routing.RegisterRoute(Routes.ChargesFor, typeof(ChargesPage));
        Routing.RegisterRoute(Routes.ReadingsFor, typeof(ReadingsPage));
        Routing.RegisterRoute(Routes.VatRates, typeof(VatRatesPage));
        Routing.RegisterRoute(Routes.VatRateEdit, typeof(VatRateEditPage));
        Routing.RegisterRoute(Routes.BillEdit, typeof(BillEditPage));
        Routing.RegisterRoute(Routes.BillsFor, typeof(BillsPage));
        Routing.RegisterRoute(Routes.BillTemplates, typeof(BillTemplatesPage));
        Routing.RegisterRoute(Routes.BillTemplateEdit, typeof(BillTemplateEditPage));
        Routing.RegisterRoute(Routes.ImportWizard, typeof(ImportWizardPage));
        Routing.RegisterRoute(Routes.Imports, typeof(ImportsPage));
        Routing.RegisterRoute(Routes.SettingsSection, typeof(SettingsSectionPage));
        Routing.RegisterRoute(Routes.PaymentEdit, typeof(PaymentEditPage));
        Routing.RegisterRoute(Routes.PaymentsFor, typeof(PaymentsPage));
        Routing.RegisterRoute(Routes.EqualizedSettlement, typeof(EqualizedSettlementPage));
        BindingContext = viewModel;
        Navigating += OnNavigating;
        ApplyToolbarColors();
        ApplyStatusBar();
#if WINDOWS
        Loaded += (_, _) => HookChromeIconScale();
#endif
        if (Application.Current is { } app)
        {
            app.RequestedThemeChanged += (_, _) => ApplyStatusBar();
        }
    }

    // Toolbar colours (R-01, C-17). Android: blue / dark bar with light icons. Windows keeps its default toolbar background,
    // but Shell.ForegroundColor is also the colour of the Back and ☰ buttons, which stand on the window background (MAUI
    // MauiToolbar.UpdateIconColor sets their foreground and their PointerOver / Pressed / Disabled colours from it). Unset, WinUI
    // takes the hover colours from the system theme, which may differ from the app theme: in the dark app theme Back
    // vanished after a hover (owner, 2026-11-05). So on Windows it is the theme's text colour — readable on the window
    // background in both themes, also while hovered.
    private void ApplyToolbarColors()
    {
#if WINDOWS
        if (Application.Current?.Resources is { } windowsResources
            && windowsResources.TryGetValue("TextLight", out var textLightValue) && textLightValue is Color textLight
            && windowsResources.TryGetValue("TextDark", out var textDarkValue) && textDarkValue is Color textDark)
        {
            this.SetAppThemeColor(Shell.ForegroundColorProperty, textLight, textDark);
            this.SetAppThemeColor(Shell.TitleColorProperty, textLight, textDark);
        }
#elif ANDROID
        var resources = Application.Current?.Resources;
        if (resources is null
            || !resources.TryGetValue("ToolbarLight", out var barLight) || barLight is not Color toolbarLight
            || !resources.TryGetValue("ToolbarDark", out var barDark) || barDark is not Color toolbarDark
            || !resources.TryGetValue("OnToolbarLight", out var onLight) || onLight is not Color onToolbarLight
            || !resources.TryGetValue("OnToolbarDark", out var onDark) || onDark is not Color onToolbarDark)
        {
            return;
        }

        this.SetAppThemeColor(Shell.BackgroundColorProperty, toolbarLight, toolbarDark);
        this.SetAppThemeColor(Shell.ForegroundColorProperty, onToolbarLight, onToolbarDark);
        this.SetAppThemeColor(Shell.TitleColorProperty, onToolbarLight, onToolbarDark);
#endif
    }

#if WINDOWS
    // Sharp menu and toolbar icons on Windows. MAUI renders a FontImageSource on Windows as a bitmap at 96 dpi whatever the
    // display scale (dotnet/maui#1000), and both the flyout (an Image of 16 × 16, MAUI BaseShellItem default template) and
    // the toolbar (an ImageIcon in the AppBarButton icon slot) squeeze it into a 16 × 16 DIP box: a 20 / 24 px glyph is
    // resampled and looks blurred, and on a 125 %+ display it is stretched as well. So these icons are rendered at
    // exactly 16 DIP × display scale physical pixels (100 % → 16, 150 % → 24, 200 % → 32) and re-rendered when the window
    // moves to a display with another scale. Android is not affected (the glyph is drawn at the device density).
    private const double ChromeIconBoxSize = 16;
    private static readonly string[] ToolbarIconKeys = ["IconHelpAccentToolbarSource", "IconSaveAccentToolbarSource"];
    private Microsoft.UI.Xaml.XamlRoot? _chromeIconRoot;
    private double _chromeIconPixels;

    private void HookChromeIconScale()
    {
        if (Handler?.PlatformView is not Microsoft.UI.Xaml.FrameworkElement { XamlRoot: { } root }
            || ReferenceEquals(root, _chromeIconRoot))
        {
            return;
        }

        _chromeIconRoot = root;
        // Changed also fires on every resize; ApplyChromeIconScale returns at once when the pixel size stays the same.
        root.Changed += (sender, _) => ApplyChromeIconScale(sender.RasterizationScale);
        ApplyChromeIconScale(root.RasterizationScale);
    }

    private void ApplyChromeIconScale(double scale)
    {
        var pixels = Math.Round(ChromeIconBoxSize * (scale > 0 ? scale : 1));
        if (pixels == _chromeIconPixels)
        {
            return;
        }

        _chromeIconPixels = pixels;

        // Flyout: the Image listens to changes of its source, so a new size re-renders the icon in place (the colour keeps
        // its AppThemeBinding).
        foreach (var item in Items)
        {
            if (item.Icon is FontImageSource icon)
            {
                icon.Size = pixels;
            }
        }

        // Toolbar: the shared sources are used by every page created from now on.
        if (Application.Current?.Resources is { } resources)
        {
            foreach (var key in ToolbarIconKeys)
            {
                if (resources.TryGetValue(key, out var value) && value is FontImageSource source)
                {
                    source.Size = pixels;
                }
            }
        }

        // The Windows toolbar renders an icon only when ToolbarItem.IconImageSource changes, so the visible page re-sets
        // its icons. Pages further back in the stack keep the old rendering until they are opened again (only after a move
        // to a display with another scale; the icon stays in its box, it is just less sharp).
        if (CurrentPage is { } page)
        {
            foreach (var toolbarItem in page.ToolbarItems)
            {
                if (toolbarItem.IconImageSource is FontImageSource current)
                {
                    toolbarItem.IconImageSource = null;
                    toolbarItem.IconImageSource = current;
                }
            }
        }
    }
#endif

    // The status bar takes the toolbar colour in the current theme (R-01); content colour is the opposite of the bar.
    // Android only (Windows has no status bar API: CA1416).
    private static void ApplyStatusBar()
    {
#if ANDROID
        try
        {
            var dark = Application.Current?.RequestedTheme == AppTheme.Dark;
            if (Application.Current?.Resources.TryGetValue(dark ? "ToolbarDark" : "ToolbarLight", out var value) == true && value is Color color)
            {
                CommunityToolkit.Maui.Core.Platform.StatusBar.SetColor(color);
                CommunityToolkit.Maui.Core.Platform.StatusBar.SetStyle(
                    CommunityToolkit.Maui.Core.StatusBarStyle.LightContent);
            }
        }
        catch (Exception)
        {
            // Cosmetic only: a platform without a status bar API must not stop the app.
        }
#endif
    }

    // One central check for every way out of a page: menu item, toolbar / hardware Back, in-app "..". Pushing a page
    // keeps the current one alive, so it is not guarded.
    private async void OnNavigating(object? sender, ShellNavigatingEventArgs e)
    {
        if (_bypassGuard || e.Source is not (ShellNavigationSource.Pop
            or ShellNavigationSource.PopToRoot
            or ShellNavigationSource.ShellItemChanged
            or ShellNavigationSource.ShellSectionChanged
            or ShellNavigationSource.ShellContentChanged))
        {
            return;
        }

        var sectionSwitch = e.Source is not (ShellNavigationSource.Pop or ShellNavigationSource.PopToRoot);
        var source = CurrentItem?.CurrentItem;
        var deferral = e.GetDeferral();
        try
        {
            if (!await _guard.CanLeaveAsync())
            {
                e.Cancel();
                return;
            }

            // Leaving a section closes everything that was pushed inside it, so the menu always opens its list.
            if (sectionSwitch && source is { Stack.Count: > 1 })
            {
                _bypassGuard = true;
                try
                {
                    await source.Navigation.PopToRootAsync(false);
                }
                finally
                {
                    _bypassGuard = false;
                }
            }
        }
        finally
        {
            deferral.Complete();
        }
    }
}
