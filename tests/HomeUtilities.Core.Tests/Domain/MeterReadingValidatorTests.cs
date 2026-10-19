using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using static HomeUtilities.Core.Tests.Fakes.MeterBuilder;

namespace HomeUtilities.Core.Tests.Domain;

public class MeterReadingValidatorTests
{
    private readonly Meter _meter = Meter(D(2020, 1, 1), 5);

    private MeterRegister Register => _meter.Registers[0];

    private string[] Codes(MeterReading r) => [.. MeterReadingValidator.Validate(r, Register, _meter).Errors.Select(e => e.Code)];

    private MeterReading New(DateOnly date, decimal value = 1, ReadingKind kind = ReadingKind.Regular) =>
        new() { RegisterId = Register.Id, Date = date, Value = value, Kind = kind };

    [Fact]
    public void Valid_reading_passes() => Assert.Empty(Codes(New(D(2020, 6, 1), 12.5m)));

    [Fact]
    public void Negative_value_fails_and_zero_passes()
    {
        Assert.Equal([ErrorCodes.MeterReadingValueNegative], Codes(New(D(2020, 6, 1), -0.001m)));
        Assert.Empty(Codes(New(D(2020, 6, 1), 0)));
    }

    [Fact]
    public void Date_must_be_within_installation()
    {
        _meter.RemovedOn = D(2020, 12, 31);
        Assert.Equal([ErrorCodes.MeterReadingDateOutsideInstallation], Codes(New(D(2019, 12, 31))));
        Assert.Empty(Codes(New(D(2020, 1, 1))));
        Assert.Empty(Codes(New(D(2020, 12, 31))));
        Assert.Equal([ErrorCodes.MeterReadingDateOutsideInstallation], Codes(New(D(2021, 1, 1))));
    }

    [Fact]
    public void Second_initial_fails()
    {
        _meter.Add(D(2020, 1, 1), 0, ReadingKind.Initial);
        Assert.Contains(ErrorCodes.MeterReadingDuplicateInitial, Codes(New(D(2020, 1, 1), 0, ReadingKind.Initial)));
    }

    [Fact]
    public void Initial_must_be_earliest()
    {
        _meter.Add(D(2020, 3, 1), 5);
        Assert.Equal([ErrorCodes.MeterReadingInitialNotEarliest], Codes(New(D(2020, 4, 1), 0, ReadingKind.Initial)));
        Assert.Empty(Codes(New(D(2020, 3, 1), 0, ReadingKind.Initial)));
        Assert.Empty(Codes(New(D(2020, 2, 1), 0, ReadingKind.Initial)));
    }

    [Fact]
    public void Reading_before_existing_initial_fails()
    {
        _meter.Add(D(2020, 3, 1), 0, ReadingKind.Initial);
        Assert.Equal([ErrorCodes.MeterReadingInitialNotEarliest], Codes(New(D(2020, 2, 1))));
    }

    [Fact]
    public void Second_final_fails()
    {
        _meter.Add(D(2020, 5, 1), 9, ReadingKind.Final);
        Assert.Contains(ErrorCodes.MeterReadingDuplicateFinal, Codes(New(D(2020, 5, 1), 9, ReadingKind.Final)));
    }

    [Fact]
    public void Final_must_be_latest()
    {
        _meter.Add(D(2020, 5, 1), 5);
        Assert.Equal([ErrorCodes.MeterReadingFinalNotLatest], Codes(New(D(2020, 4, 1), 9, ReadingKind.Final)));
        Assert.Empty(Codes(New(D(2020, 6, 1), 9, ReadingKind.Final)));
    }

    [Fact]
    public void Reading_after_existing_final_fails()
    {
        _meter.Add(D(2020, 5, 1), 9, ReadingKind.Final);
        Assert.Equal([ErrorCodes.MeterReadingFinalNotLatest], Codes(New(D(2020, 6, 1))));
    }

    [Fact]
    public void Final_must_be_dated_on_removal()
    {
        _meter.RemovedOn = D(2020, 6, 1);
        Assert.Equal([ErrorCodes.MeterReadingFinalDateMismatch], Codes(New(D(2020, 5, 31), 9, ReadingKind.Final)));
        Assert.Empty(Codes(New(D(2020, 6, 1), 9, ReadingKind.Final)));
    }

    [Fact]
    public void Same_date_and_kind_fails_but_other_kind_or_deleted_passes()
    {
        _meter.Add(D(2020, 6, 1), 5);
        Assert.Equal([ErrorCodes.MeterReadingDuplicateOnDate], Codes(New(D(2020, 6, 1))));
        Assert.Empty(Codes(New(D(2020, 6, 1), 5, ReadingKind.Control)));
        Register.Readings[0].IsDeleted = true;
        Assert.Empty(Codes(New(D(2020, 6, 1))));
    }

    [Fact]
    public void Reading_does_not_conflict_with_itself()
    {
        var r = _meter.Add(D(2020, 1, 1), 0, ReadingKind.Initial);
        Assert.Empty(Codes(r));
    }
}
