using System.Globalization;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Backup;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Import;
using HomeUtilities.Core.PdfBills;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels.Tests;

/// <summary>T-057: the collapsible card state and the header summaries of the Imports and Backup screens.</summary>
public class CollapsibleCardTests
{
    private readonly FakeLanguageService _language = new();
    private readonly FakeSettingsStore _store = new();

    private CollapsibleCardViewModel Card(string key, bool defaultExpanded, FakeSettingsStore? store = null) =>
        new(store ?? _store, new FakeLocalizer(_language), key, defaultExpanded);

    // ---- expanded state ----

    [Fact]
    public void A_new_card_uses_its_default_state()
    {
        Assert.False(Card("A", defaultExpanded: false).IsExpanded);
        Assert.True(Card("B", defaultExpanded: true).IsExpanded);
        Assert.Empty(_store.Values);
    }

    [Fact]
    public void Toggling_saves_the_state_under_a_key_of_its_own_and_a_new_view_model_reads_it_back()
    {
        var card = Card("Imports.PdfLayouts", defaultExpanded: false);

        card.ToggleCommand.Execute(null);

        Assert.True(card.IsExpanded);
        Assert.Equal("1", _store.Values["Card.Expanded.Imports.PdfLayouts"]);
        Assert.True(Card("Imports.PdfLayouts", defaultExpanded: false).IsExpanded);

        card.ToggleCommand.Execute(null);

        Assert.Equal("0", _store.Values["Card.Expanded.Imports.PdfLayouts"]);
        Assert.False(Card("Imports.PdfLayouts", defaultExpanded: true).IsExpanded); // a stored "closed" beats the default "open"
    }

    [Fact]
    public void Two_cards_are_independent()
    {
        var first = Card("One", defaultExpanded: false);
        var second = Card("Two", defaultExpanded: false);

        first.ToggleCommand.Execute(null);

        Assert.True(first.IsExpanded);
        Assert.False(second.IsExpanded);
        Assert.False(Card("Two", defaultExpanded: false).IsExpanded);
    }

    [Fact]
    public void Texts_set_the_title_the_summary_and_the_screen_reader_description()
    {
        var card = Card("A", defaultExpanded: false);
        Assert.False(card.HasSummary);

        card.SetTexts("Saved profiles", "3 profiles");

        Assert.Equal("Saved profiles", card.Title);
        Assert.Equal("3 profiles", card.Summary);
        Assert.True(card.HasSummary);
        Assert.False(card.SummaryIsError);
        Assert.Equal("CollapsibleCardToggle Saved profiles [en]", card.ToggleDescription);

        card.SetTexts("Saved profiles", string.Empty);
        Assert.False(card.HasSummary);
    }

    // ---- summary texts (real resource files) ----

    [Theory]
    [InlineData(0, "нет")]
    [InlineData(1, "1 профиль")]
    [InlineData(2, "2 профиля")]
    [InlineData(5, "5 профилей")]
    [InlineData(11, "11 профилей")]
    [InlineData(21, "21 профиль")]
    public void Profiles_in_Russian(int count, string expected)
    {
        CultureInfo.CurrentUICulture = new CultureInfo("ru-RU");
        Assert.Equal(expected, CardSummaryText.Count(ResxLocalizer.Russian, "CardSummaryProfiles", count));
    }

    [Theory]
    [InlineData(0, "none")]
    [InlineData(1, "1 profile")]
    [InlineData(2, "2 profiles")]
    [InlineData(5, "5 profiles")]
    [InlineData(21, "21 profiles")]
    public void Profiles_in_English(int count, string expected)
    {
        CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        Assert.Equal(expected, CardSummaryText.Count(ResxLocalizer.English, "CardSummaryProfiles", count));
    }

    [Theory]
    [InlineData(1, "1 макет")]
    [InlineData(3, "3 макета")]
    [InlineData(7, "7 макетов")]
    public void Layouts_in_Russian(int count, string expected)
    {
        CultureInfo.CurrentUICulture = new CultureInfo("ru-RU");
        Assert.Equal(expected, CardSummaryText.Count(ResxLocalizer.Russian, "CardSummaryLayouts", count));
    }

    [Fact]
    public void The_import_history_names_the_count_and_the_last_date()
    {
        // 12:00 UTC is the same calendar day in every time zone the tests can run in.
        var last = new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

        CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        Assert.Equal(
            $"12 imports · last {last.ToLocalTime().ToString("d", new CultureInfo("en-US"))}",
            CardSummaryText.CountAndLastDate(ResxLocalizer.English, "CardSummaryImports", 12, last));

        CultureInfo.CurrentUICulture = new CultureInfo("ru-RU");
        Assert.Equal(
            $"12 импортов · последний: {last.ToLocalTime().ToString("d", new CultureInfo("ru-RU"))}",
            CardSummaryText.CountAndLastDate(ResxLocalizer.Russian, "CardSummaryImports", 12, last));
        Assert.Equal("нет", CardSummaryText.CountAndLastDate(ResxLocalizer.Russian, "CardSummaryImports", 0, null));
    }

    [Fact]
    public void The_copies_name_the_count_and_the_last_date_with_the_time()
    {
        var last = new DateTimeOffset(2026, 11, 16, 3, 0, 0, TimeSpan.Zero);
        CultureInfo.CurrentUICulture = new CultureInfo("ru-RU");

        Assert.Equal(
            $"8 копий · последняя: {last.ToLocalTime().ToString("g", new CultureInfo("ru-RU"))}",
            CardSummaryText.CountAndLastTime(ResxLocalizer.Russian, "CardSummaryCopies", 8, last));
        Assert.Equal(
            "2 копии · последняя: " + last.ToLocalTime().ToString("g", new CultureInfo("ru-RU")),
            CardSummaryText.CountAndLastTime(ResxLocalizer.Russian, "CardSummaryCopies", 2, last));
    }

    [Fact]
    public void Every_summary_key_exists_in_both_languages()
    {
        string[] keys =
        [
            "CollapsibleCardToggle", "CardSummaryNone", "CardSummaryWithLastDate", "CardSummaryWithLastTime", "CardSummaryRepairNone",
            "CardSummaryRepairFound", "CardSummaryAutoOn", "CardSummaryAutoOff",
            .. new[] { "Imports", "Profiles", "Layouts", "Copies" }.SelectMany(n => new[] { "One", "Many" }.Select(f => $"CardSummary{n}_{f}")),
        ];
        foreach (var key in keys)
        {
            Assert.True(ResxLocalizer.English.TryGet(key, out _), $"EN {key}");
            Assert.True(ResxLocalizer.Russian.TryGet(key, out _), $"RU {key}");
        }

        foreach (var name in new[] { "Imports", "Profiles", "Layouts", "Copies" })
        {
            Assert.True(ResxLocalizer.Russian.TryGet($"CardSummary{name}_Few", out _), $"RU {name} Few");
        }
    }
}
