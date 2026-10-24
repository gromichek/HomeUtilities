using HomeUtilities.Core.Attachments;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.ViewModels.Tests;

public class ReadingPhotoTests
{
    private static readonly DateOnly Today = new(2026, 10, 24);

    private readonly ContractTestKit _kit = new(Today);

    private Meter AddMeter(string location, params RegisterKind[] kinds)
    {
        var meter = new Meter
        {
            PropertyId = _kit.Property.Id,
            Resource = MeterResource.Electricity,
            Unit = MeterUnit.KWh,
            Location = location,
            InstalledOn = new DateOnly(2020, 1, 1),
        };
        foreach (var kind in kinds.Length == 0 ? [RegisterKind.Total] : kinds)
        {
            meter.Registers.Add(new MeterRegister { MeterId = meter.Id, Kind = kind, Direction = EnergyDirection.Import });
        }

        _kit.Meters.Stored.Add(meter);
        return meter;
    }

    private async Task<ReadingsViewModel> LoadAsync()
    {
        var vm = _kit.CreateReadings();
        await vm.ReloadAsync();
        return vm;
    }

    private static MeterCardViewModel Card(ReadingsViewModel vm, string title) =>
        vm.Groups.SelectMany(g => g.Cards).Single(c => c.Title == title);

    private async Task PickAsync(MeterCardViewModel card, PickedFile? file)
    {
        _kit.Media.Next = file;
        _kit.Dialogs.ChooseScript = options => options[0];
        await card.PhotoCommand.ExecuteAsync(null);
    }

    [Fact]
    public async Task Photo_is_attached_to_the_first_saved_reading_of_its_meter()
    {
        var meter = AddMeter("Main", RegisterKind.Day, RegisterKind.Night);
        var other = AddMeter("Garage");
        var vm = await LoadAsync();
        var card = Card(vm, "Main");
        await PickAsync(card, FakeMediaService.Photo("IMG_1.jpg", "image/jpeg", 7, 8, 9));
        card.Rows[0].ValueText = "100";
        card.Rows[1].ValueText = "50";
        Card(vm, "Garage").Rows[0].ValueText = "10";

        await vm.SaveAllCommand.ExecuteAsync(null);

        var (attachment, bytes) = Assert.Single(_kit.Attachments.Added);
        var dayReading = _kit.Meters.ReadingsOf(meter.Registers.Single(r => r.Kind == RegisterKind.Day).Id).Single();
        Assert.Equal(dayReading.Id, attachment.OwnerId);
        Assert.Equal(AttachmentOwnerKind.MeterReading, attachment.OwnerKind);
        Assert.Equal(("IMG_1.jpg", "image/jpeg"), (attachment.FileName, attachment.ContentType));
        Assert.Equal([7, 8, 9], bytes);
        Assert.Equal(attachment.Id, dayReading.PhotoAttachmentId);
        Assert.Null(_kit.Meters.ReadingsOf(meter.Registers.Single(r => r.Kind == RegisterKind.Night).Id).Single().PhotoAttachmentId);
        Assert.Null(_kit.Meters.ReadingsOf(other.Registers[0].Id).Single().PhotoAttachmentId);
    }

    [Fact]
    public async Task Photo_is_cleared_from_the_card_after_saving()
    {
        AddMeter("Main");
        var vm = await LoadAsync();
        await PickAsync(Card(vm, "Main"), FakeMediaService.Photo());
        Card(vm, "Main").Rows[0].ValueText = "100";

        await vm.SaveAllCommand.ExecuteAsync(null);

        Assert.False(Card(vm, "Main").HasPhoto);
    }

    [Fact]
    public async Task Removed_photo_is_not_saved()
    {
        var meter = AddMeter("Main");
        var vm = await LoadAsync();
        var card = Card(vm, "Main");
        await PickAsync(card, FakeMediaService.Photo());
        Assert.True(card.HasPhoto);

        card.RemovePhotoCommand.Execute(null);
        card.Rows[0].ValueText = "100";
        await vm.SaveAllCommand.ExecuteAsync(null);

        Assert.False(card.HasPhoto);
        Assert.Empty(_kit.Attachments.Added);
        Assert.Null(_kit.Meters.ReadingsOf(meter.Registers[0].Id).Single().PhotoAttachmentId);
    }

    [Fact]
    public async Task Save_without_a_photo_stores_no_attachment()
    {
        var meter = AddMeter("Main");
        var vm = await LoadAsync();
        Card(vm, "Main").Rows[0].ValueText = "100";

        await vm.SaveAllCommand.ExecuteAsync(null);

        Assert.Empty(_kit.Attachments.Added);
        Assert.Null(_kit.Meters.ReadingsOf(meter.Registers[0].Id).Single().PhotoAttachmentId);
        Assert.Single(_kit.Meters.AddedBatches);
    }

    [Fact]
    public async Task A_photo_alone_is_not_an_unsaved_value_and_stays_on_its_card()
    {
        AddMeter("Main");
        var vm = await LoadAsync();
        await PickAsync(Card(vm, "Main"), FakeMediaService.Photo());

        Assert.False(vm.HasUnsavedValues);
        Assert.True(await vm.CanLeaveAsync());
        Assert.True(Card(vm, "Main").HasPhoto);
        Assert.Empty(_kit.Attachments.Added);
    }

    [Fact]
    public async Task A_photo_of_a_meter_without_a_new_value_is_kept_for_later()
    {
        AddMeter("Main");
        AddMeter("Garage");
        var vm = await LoadAsync();
        await PickAsync(Card(vm, "Garage"), FakeMediaService.Photo());
        Card(vm, "Main").Rows[0].ValueText = "100";

        await vm.SaveAllCommand.ExecuteAsync(null);

        Assert.Empty(_kit.Attachments.Added);
        Assert.True(Card(vm, "Garage").HasPhoto);
    }

    [Fact]
    public async Task The_photo_survives_a_reload_of_the_screen()
    {
        AddMeter("Main");
        var vm = await LoadAsync();
        await PickAsync(Card(vm, "Main"), FakeMediaService.Photo("keep.jpg"));

        await vm.OnAppearingAsync();

        Assert.Equal("keep.jpg", Card(vm, "Main").PhotoName);
    }

    [Fact]
    public async Task Dont_save_drops_the_photo()
    {
        AddMeter("Main");
        var vm = await LoadAsync();
        var card = Card(vm, "Main");
        await PickAsync(card, FakeMediaService.Photo());
        card.Rows[0].ValueText = "100";
        _kit.Dialogs.UnsavedChoice = UnsavedChangesChoice.DontSave;

        Assert.True(await vm.CanLeaveAsync());

        Assert.False(Card(vm, "Main").HasPhoto);
    }

    [Fact]
    public async Task Camera_or_gallery_follows_the_choice_in_the_sheet()
    {
        AddMeter("Main");
        var vm = await LoadAsync();
        var card = Card(vm, "Main");
        _kit.Media.Next = FakeMediaService.Photo();

        _kit.Dialogs.ChooseScript = options => options[0];
        await card.PhotoCommand.ExecuteAsync(null);
        Assert.Equal((1, 0), (_kit.Media.CaptureCalls, _kit.Media.PickCalls));

        _kit.Dialogs.ChooseScript = options => options[1];
        await card.PhotoCommand.ExecuteAsync(null);
        Assert.Equal((1, 1), (_kit.Media.CaptureCalls, _kit.Media.PickCalls));
    }

    [Fact]
    public async Task Without_a_camera_the_file_picker_opens_directly()
    {
        AddMeter("Main");
        _kit.Media.IsCaptureSupported = false;
        _kit.Media.Next = FakeMediaService.Photo("pc.jpg");
        var vm = await LoadAsync();
        var asked = false;
        _kit.Dialogs.ChooseScript = _ =>
        {
            asked = true;
            return null;
        };

        await Card(vm, "Main").PhotoCommand.ExecuteAsync(null);

        Assert.False(asked);
        Assert.Equal((0, 1), (_kit.Media.CaptureCalls, _kit.Media.PickCalls));
        Assert.Equal("pc.jpg", Card(vm, "Main").PhotoName);
    }

    [Fact]
    public async Task Cancelling_the_sheet_or_the_picker_does_nothing()
    {
        AddMeter("Main");
        var vm = await LoadAsync();
        var card = Card(vm, "Main");

        _kit.Dialogs.ChooseScript = _ => null;
        await card.PhotoCommand.ExecuteAsync(null);
        Assert.Equal((0, 0), (_kit.Media.CaptureCalls, _kit.Media.PickCalls));

        _kit.Dialogs.ChooseScript = options => options[0];
        _kit.Media.Next = null;
        await card.PhotoCommand.ExecuteAsync(null);

        Assert.False(card.HasPhoto);
        Assert.Empty(_kit.Dialogs.Alerts);
    }

    [Fact]
    public async Task Denied_camera_permission_shows_a_message_and_does_not_crash()
    {
        AddMeter("Main");
        var vm = await LoadAsync();
        _kit.Media.Failure = new MediaPermissionDeniedException();
        _kit.Dialogs.ChooseScript = options => options[0];

        await Card(vm, "Main").PhotoCommand.ExecuteAsync(null);

        var alert = Assert.Single(_kit.Dialogs.Alerts);
        Assert.Contains("PhotoPermissionDenied", alert.Message);
        Assert.False(Card(vm, "Main").HasPhoto);
    }

    [Fact]
    public async Task A_file_type_that_is_not_allowed_is_refused_with_a_message()
    {
        AddMeter("Main");
        var vm = await LoadAsync();

        await PickAsync(Card(vm, "Main"), FakeMediaService.Photo("movie.gif", "image/gif"));

        Assert.Contains("Error_attachment_content_type_not_allowed", Assert.Single(_kit.Dialogs.Alerts).Message);
        Assert.False(Card(vm, "Main").HasPhoto);
    }

    [Fact]
    public async Task A_file_over_the_size_limit_is_refused_when_the_stream_knows_its_length()
    {
        AddMeter("Main");
        var vm = await LoadAsync();
        var huge = new PickedFile("huge.jpg", "image/jpeg", () => Task.FromResult<Stream>(new SizedStream(AttachmentValidator.MaxSizeBytes + 1)));

        await PickAsync(Card(vm, "Main"), huge);

        Assert.Contains("Error_attachment_too_large", Assert.Single(_kit.Dialogs.Alerts).Message);
        Assert.False(Card(vm, "Main").HasPhoto);
    }

    [Fact]
    public async Task A_file_the_store_rejects_stops_the_save_and_drops_the_photo()
    {
        var meter = AddMeter("Main");
        var vm = await LoadAsync();
        var card = Card(vm, "Main");
        await PickAsync(card, FakeMediaService.Photo());
        card.Rows[0].ValueText = "100";
        _kit.Attachments.Rejection = ValidationResult.Fail(new ValidationError(ErrorCodes.AttachmentTooLarge, "SizeBytes", [20L]));

        await vm.SaveAllCommand.ExecuteAsync(null);

        Assert.Contains("Error_attachment_too_large", Assert.Single(_kit.Dialogs.Alerts).Message);
        Assert.Empty(_kit.Meters.AddedBatches);
        Assert.False(card.HasPhoto);
        Assert.Equal("100", card.Rows[0].ValueText);
        Assert.Empty(_kit.Meters.ReadingsOf(meter.Registers[0].Id));
    }

    [Fact]
    public async Task A_failed_save_of_the_readings_removes_the_stored_photo_again()
    {
        AddMeter("Main");
        var vm = await LoadAsync();
        var card = Card(vm, "Main");
        await PickAsync(card, FakeMediaService.Photo());
        card.Rows[0].ValueText = "100";
        _kit.Meters.AddReadingsFailure = new InvalidOperationException("disk full");

        await Assert.ThrowsAsync<InvalidOperationException>(() => vm.SaveAllCommand.ExecuteAsync(null));

        var (attachment, _) = Assert.Single(_kit.Attachments.Added);
        Assert.Equal([attachment.Id], _kit.Attachments.Deleted);
    }

    /// <summary>A stream that only reports a length.</summary>
    private sealed class SizedStream(long length) : MemoryStream
    {
        public override long Length => length;
    }
}
