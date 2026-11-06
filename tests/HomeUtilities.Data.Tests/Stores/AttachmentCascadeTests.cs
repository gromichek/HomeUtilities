using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Attachments;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Domain;
using HomeUtilities.Data.Stores;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data.Tests.Stores;

public sealed class AttachmentCascadeTests : IDisposable
{
    private readonly TestDb _test = new();

    public void Dispose() => _test.Dispose();

    private AttachmentStore Attachments => new(_test.Factory, Path.Combine(Path.GetTempPath(), "hu-cascade-" + _test.GetHashCode()));

    private async Task<Attachment> AddAsync(AttachmentOwnerKind kind, Guid ownerId) =>
        await Attachments.AddAsync(new Attachment { FileName = "p.jpg", ContentType = "image/jpeg", OwnerKind = kind, OwnerId = ownerId }, new MemoryStream(new byte[10]));

    private async Task<bool> IsDeletedAsync(Guid attachmentId)
    {
        await using var db = _test.NewContext();
        return (await db.Attachments.IgnoreQueryFilters().SingleAsync(a => a.Id == attachmentId)).IsDeleted;
    }

    private async Task<Bill> SaveBillAsync()
    {
        var (propertyId, categoryId) = await AggregateMergerTests.PrepareBillOwnersAsync(_test);
        var bill = new Bill { PropertyId = propertyId, CategoryId = categoryId, Period = new(2026, 9), Gross = 10m, Kind = BillKind.OneOff, Title = "x" };
        bill.Lines.Add(new BillLine { BillId = bill.Id, Description = "A", Gross = 10m, SortOrder = 1 });
        await new BillStore(_test.Factory).SaveAsync(bill);
        return bill;
    }

    [Fact]
    public async Task Deleting_a_bill_soft_deletes_its_photo_but_not_the_photo_of_another_bill()
    {
        var bill = await SaveBillAsync();
        var photo = await AddAsync(AttachmentOwnerKind.Bill, bill.Id);
        var foreign = await AddAsync(AttachmentOwnerKind.Bill, Guid.NewGuid());

        await new BillStore(_test.Factory).DeleteAsync(bill.Id);

        Assert.True(await IsDeletedAsync(photo.Id));
        Assert.False(await IsDeletedAsync(foreign.Id));
        Assert.Null(await Attachments.GetAsync(photo.Id));
    }

    [Fact]
    public async Task Restoring_the_owner_restores_the_attachments_deleted_with_it_but_not_one_removed_earlier()
    {
        var bill = await SaveBillAsync();
        var removedEarlier = await AddAsync(AttachmentOwnerKind.Bill, bill.Id);
        await Attachments.DeleteAsync(removedEarlier.Id);
        _test.Clock.UtcNow = _test.Clock.UtcNow.AddMinutes(1);
        var photo = await AddAsync(AttachmentOwnerKind.Bill, bill.Id);
        _test.Clock.UtcNow = _test.Clock.UtcNow.AddMinutes(1);
        await new BillStore(_test.Factory).DeleteAsync(bill.Id);
        DateTimeOffset deletedAt;
        await using (var db = _test.NewContext())
        {
            deletedAt = (await db.Bills.IgnoreQueryFilters().SingleAsync(b => b.Id == bill.Id)).UpdatedAt;
        }

        // The owner's own restore (Undo) is the caller's; the attachments come back through the one shared method.
        await Attachments.RestoreByOwnerAsync(AttachmentOwnerKind.Bill, bill.Id, deletedAt);

        Assert.False(await IsDeletedAsync(photo.Id));
        Assert.True(await IsDeletedAsync(removedEarlier.Id));
    }

    [Fact]
    public async Task Deleting_a_reading_deletes_its_photo()
    {
        var meter = await SaveMeterWithReadingAsync();
        var reading = meter.Registers[0].Readings[0];
        var photo = await AddAsync(AttachmentOwnerKind.MeterReading, reading.Id);

        await new MeterStore(_test.Factory).DeleteReadingAsync(reading.Id);

        Assert.True(await IsDeletedAsync(photo.Id));
    }

    [Fact]
    public async Task Deleting_a_meter_deletes_its_own_photos_and_those_of_its_readings()
    {
        var meter = await SaveMeterWithReadingAsync();
        var readingPhoto = await AddAsync(AttachmentOwnerKind.MeterReading, meter.Registers[0].Readings[0].Id);
        var meterPhoto = await AddAsync(AttachmentOwnerKind.Meter, meter.Id);

        await new MeterStore(_test.Factory).DeleteAsync(meter.Id);

        Assert.True(await IsDeletedAsync(readingPhoto.Id));
        Assert.True(await IsDeletedAsync(meterPhoto.Id));
    }

    [Fact]
    public async Task Deleting_by_owner_works_for_an_owner_deleted_by_other_means()
    {
        var owner = Guid.NewGuid();
        var photo = await AddAsync(AttachmentOwnerKind.Other, owner);

        await Attachments.DeleteByOwnerAsync(AttachmentOwnerKind.Other, owner);

        Assert.True(await IsDeletedAsync(photo.Id));
    }

    private async Task<Meter> SaveMeterWithReadingAsync()
    {
        var property = new Property { Name = "P" };
        await using (var db = _test.NewContext())
        {
            db.Add(property);
            await db.SaveChangesAsync();
        }

        var meter = new Meter { PropertyId = property.Id, InstalledOn = new DateOnly(2026, 1, 1) };
        var register = new MeterRegister { MeterId = meter.Id };
        register.Readings.Add(new MeterReading { RegisterId = register.Id, Date = new DateOnly(2026, 2, 1), Value = 5m });
        meter.Registers.Add(register);
        await new MeterStore(_test.Factory).SaveAsync(meter);
        return meter;
    }
}
