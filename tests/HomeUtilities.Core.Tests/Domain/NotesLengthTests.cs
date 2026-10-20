using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Tariffs;
using static HomeUtilities.Core.Tests.Fakes.MeterBuilder;

namespace HomeUtilities.Core.Tests.Domain;

/// <summary>Every validator rejects comments longer than 2000 characters (2000 passes, 2001 fails).</summary>
public class NotesLengthTests
{
    private static string Text(int length) => new('n', length);

    private static void AssertLimit(Func<string, ValidationResult> validate, string field)
    {
        Assert.True(validate(Text(2000)).IsValid);
        var error = Assert.Single(validate(Text(2001)).Errors);
        Assert.Equal((ErrorCodes.NotesTooLong, field), (error.Code, error.Field));
    }

    [Fact]
    public void Property_notes_limit() =>
        AssertLimit(n => PropertyValidator.Validate(new Property { Name = "P", Notes = n }), "Notes");

    [Fact]
    public void PropertyParameter_notes_limit() =>
        AssertLimit(n => PropertyParameterValidator.Validate(new PropertyParameter { Kind = PropertyParameterKind.TotalArea, Note = n }), "Note");

    [Fact]
    public void Property_reports_parameter_note_with_index() =>
        Assert.Equal("Parameters[0].Note", Assert.Single(PropertyValidator.Validate(new Property
        {
            Name = "P",
            Parameters = [new PropertyParameter { Kind = PropertyParameterKind.TotalArea, ValidFrom = D(2026, 1, 1), Note = Text(2001) }],
        }).Errors).Field);

    [Fact]
    public void Supplier_notes_limit() =>
        AssertLimit(n => SupplierValidator.Validate(new Supplier { Name = "S", Notes = n }), "Notes");

    [Fact]
    public void Contract_notes_limit() =>
        AssertLimit(n => ContractValidator.Validate(new Contract
        {
            PropertyId = Guid.CreateVersion7(),
            SupplierId = Guid.CreateVersion7(),
            CategoryId = Guid.CreateVersion7(),
            Title = "C",
            Notes = n,
        }), "Notes");

    [Fact]
    public void Meter_notes_limit() =>
        AssertLimit(n =>
        {
            var meter = Meter(D(2020, 1, 1));
            meter.Notes = n;
            return MeterValidator.Validate(meter);
        }, "Notes");

    [Fact]
    public void MeterReading_notes_limit() =>
        AssertLimit(n =>
        {
            var meter = Meter(D(2020, 1, 1));
            var reading = meter.Add(D(2026, 1, 1), 10m);
            reading.Note = n;
            return MeterReadingValidator.Validate(reading, meter.Registers[0], meter);
        }, "Note");

    private static TariffComponent Component(string? notes = null, string? rateNotes = null)
    {
        var component = new TariffComponent
        {
            ContractId = Guid.CreateVersion7(),
            Name = "Water",
            VatCode = "LV-STANDARD",
            Notes = notes,
        };
        component.Rates.Add(new TariffRate { ComponentId = component.Id, ValidFrom = D(2026, 1, 1), Price = 1m, Notes = rateNotes });
        return component;
    }

    [Fact]
    public void TariffComponent_notes_limit() =>
        AssertLimit(n => TariffComponentValidator.Validate(Component(notes: n)), "Notes");

    [Fact]
    public void TariffRate_notes_limit() =>
        AssertLimit(n => TariffComponentValidator.Validate(Component(rateNotes: n)), "Rates[0].Notes");

    [Fact]
    public void VatRate_notes_limit() =>
        AssertLimit(n => VatRateValidator.Validate(new VatRate { Code = "X", Name = "X", Notes = n }), "Notes");
}
