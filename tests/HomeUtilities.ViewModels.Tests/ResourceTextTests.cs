using System.Globalization;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Sync;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels.Tests;

/// <summary>Texts the owner reads, checked against the real resource files: month names, sync details, money and the two languages' keys.</summary>
public class ResourceTextTests
{
    [Fact]
    public void ShortMonthNames_English_AreThreeLetters()
    {
        var names = Enumerable.Range(1, 12).Select(m => MonthFormat.ShortName(ResxLocalizer.English, m));
        Assert.Equal(["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"], names);
    }

    [Fact]
    public void ShortMonthNames_Russian_AreThreeLettersAndEquallyLong()
    {
        var names = Enumerable.Range(1, 12).Select(m => MonthFormat.ShortName(ResxLocalizer.Russian, m)).ToList();
        Assert.Equal(["янв", "фев", "мар", "апр", "май", "июн", "июл", "авг", "сен", "окт", "ноя", "дек"], names);
        Assert.All(names, n => Assert.Equal(3, n.Length)); // .NET gives "февр.", "март", "июнь", "сент." — different lengths
    }

    [Fact]
    public void ChartAxisAndRanges_UseTheOwnNames()
    {
        Assert.Equal("сен 26", MonthFormat.ShortWithYear(ResxLocalizer.Russian, 2026, 9));
        Assert.Equal("Sep 26", MonthFormat.ShortWithYear(ResxLocalizer.English, 2026, 9));
        Assert.Equal("28 окт – 3 ноя", MonthFormat.Range(ResxLocalizer.Russian, new DateOnly(2026, 10, 28), new DateOnly(2026, 11, 3)));
        Assert.Equal("25–28 окт", MonthFormat.Range(ResxLocalizer.Russian, new DateOnly(2026, 10, 25), new DateOnly(2026, 10, 28)));
        Assert.Equal("28 Oct – 3 Nov", MonthFormat.Range(ResxLocalizer.English, new DateOnly(2026, 10, 28), new DateOnly(2026, 11, 3)));
    }

    [Fact]
    public void ReportLabels_ChartRow_UsesTheOwnNames()
    {
        var labels = new ReportLabels(ResxLocalizer.Russian, new CultureInfo("ru-RU"));
        var row = new HomeUtilities.Core.Reports.ReportRow(HomeUtilities.Core.Reports.ReportRowKind.Month, 2026, 2, [], null);
        Assert.Equal("фев 26", labels.ChartRow(row));
        Assert.Equal("июн", labels.ShortMonthName(6));
    }

    [Fact]
    public void LongMonth_IsCapitalised_AndUsesTheAppCulture()
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            Assert.Equal("November 2026", MonthFormat.Long(new YearMonth(2026, 11)));
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }

    [Fact]
    public void Money_HasANonBreakingSpaceBeforeTheCurrencySymbol()
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            Assert.Equal("1,234.50 €", BillText.Money(1234.5m, "€"));
            Assert.Equal("+0.92 €", BillText.SignedMoney(0.92m, "€"));
            Assert.Equal("+3.4 %", BillText.SignedPercent(3.4m));
            Assert.DoesNotContain(' ', BillText.Money(1234.5m, "€"));
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }

    [Fact]
    public void LabelAmountPairs_UseANonBreakingSpace_BeforeTheAmount()
    {
        // "Итого 49,22 €" must not break between the label and the amount.
        var totals = ResxLocalizer.Russian.Get("ChargesTotals", "1,00 €", "0,21 €", "1,21 €");
        Assert.Contains("Итого 1,21 €", totals);
        Assert.Contains("НДС 0,21 €", totals);
        var bills = ResxLocalizer.Russian.Get("BillTotals", "10", "5", "5");
        Assert.Equal("Выставлено 10 · Оплачено 5 · Осталось 5", bills);
    }

    [Fact]
    public void SyncDetails_AreShortLocalizedLines_WithoutFileNamesOrIds()
    {
        var at = new DateTimeOffset(2026, 11, 5, 12, 52, 0, TimeSpan.Zero);
        var time = at.ToLocalTime().ToString("HH:mm", CultureInfo.CurrentCulture);

        Assert.Equal($"{time} Получены изменения (записей: 2).", SyncDetailText.Line(ResxLocalizer.Russian, new SyncDetail(at, SyncDetailKind.AppliedChanges, 2)));
        Assert.Equal($"{time} Received changes (records: 2).", SyncDetailText.Line(ResxLocalizer.English, new SyncDetail(at, SyncDetailKind.AppliedChanges, 2)));
        Assert.Equal($"{time} Данные в облаке заменены на другом устройстве.", SyncDetailText.Line(ResxLocalizer.Russian, new SyncDetail(at, SyncDetailKind.CloudReplaced)));

        foreach (var kind in Enum.GetValues<SyncDetailKind>())
        {
            foreach (var localizer in new[] { ResxLocalizer.English, ResxLocalizer.Russian })
            {
                var line = SyncDetailText.Line(localizer, new SyncDetail(at, kind, 7));
                Assert.DoesNotContain("!SyncDetail_", line); // the key exists
                Assert.DoesNotContain(".json", line);
                Assert.DoesNotContain("/", line);
                Assert.DoesNotContain("changes/", line);
            }
        }
    }

    [Fact]
    public void SyncDetails_ManyLines_AreOnePerLine_NewestFirst()
    {
        var at = new DateTimeOffset(2026, 11, 5, 12, 52, 0, TimeSpan.Zero);
        var text = SyncDetailText.Lines(
            ResxLocalizer.Russian,
            [new SyncDetail(at.AddMinutes(1), SyncDetailKind.UploadedChanges, 3), new SyncDetail(at, SyncDetailKind.Resumed)]);
        var lines = text.Split(Environment.NewLine);
        Assert.Equal(2, lines.Length);
        Assert.EndsWith("Отправлены изменения (записей: 3).", lines[0]);
        Assert.EndsWith("Синхронизация возобновлена.", lines[1]);
    }

    [Fact]
    public void EnglishAndRussianResources_HaveTheSameKeys()
    {
        var onlyEnglish = ResxLocalizer.English.Keys.Except(ResxLocalizer.Russian.Keys).Order().ToList();
        var onlyRussian = ResxLocalizer.Russian.Keys.Except(ResxLocalizer.English.Keys).Order().ToList();
        Assert.True(onlyEnglish.Count == 0, "Only in English: " + string.Join(", ", onlyEnglish));
        Assert.True(onlyRussian.Count == 0, "Only in Russian: " + string.Join(", ", onlyRussian));
    }

    [Fact]
    public void ButtonAndEmptyTexts_FollowTheGuidelines()
    {
        Assert.Equal("Отметить оплаченным", ResxLocalizer.Russian["BillMarkPaid"]);
        Assert.Equal("Отметить оплаченным", ResxLocalizer.Russian["BillPaid"]);
        Assert.Equal("Mark as paid", ResxLocalizer.English["BillMarkPaid"]);
        Assert.Equal("быстрая оплата", ResxLocalizer.Russian["BillPaymentQuick"]);
        Assert.Equal("Отключить аккаунт Google…", ResxLocalizer.Russian["SyncDisconnectButton"]);
        Assert.Equal("Счетов пока нет. Нажмите ＋, чтобы добавить.", ResxLocalizer.Russian["BillsEmpty"]);

        foreach (var key in new[] { "BillsEmpty", "PaymentsEmpty", "PropertiesEmpty", "SuppliersEmpty", "CategoriesEmpty", "ContractsEmpty", "MetersEmpty", "VatRatesEmpty", "BillTemplatesEmpty" })
        {
            Assert.DoesNotContain("ниже", ResxLocalizer.Russian[key]);
            Assert.DoesNotContain("below", ResxLocalizer.English[key]);
            Assert.Contains("＋", ResxLocalizer.Russian[key]);
            Assert.Contains("＋", ResxLocalizer.English[key]);
        }
    }

    [Fact]
    public void ChargeWarnings_ReadWell_InRussian()
    {
        Assert.Equal("В тарифе нет цены для услуги «Водоснабжение» на 15.03.2023.", ResxLocalizer.Russian.Get("ChargeWarn_NoRate", "15.03.2023", "Водоснабжение"));
        Assert.Equal("Нет показаний за октябрь 2026 — последнее от 15.03.2023.", ResxLocalizer.Russian.Get("ChargeWarn_NoReadings", "октябрь 2026", "15.03.2023"));
        Assert.Equal("Рассчитано", ResxLocalizer.Russian["ChargeStatus_Complete"]);
        Assert.Equal("Новый счёт", ResxLocalizer.Russian["ChargesEnterBill"]);
        Assert.Equal("Замена: снят счётчик № 123 · 05.11.2026", ResxLocalizer.Russian.Get("MeterHistoryReplaced", "123", new DateOnly(2026, 11, 5)));
    }
}
