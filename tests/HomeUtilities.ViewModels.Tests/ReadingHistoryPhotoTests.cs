using HomeUtilities.Core.Attachments;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.ViewModels.Tests;

public class ReadingHistoryPhotoTests
{
    private static readonly DateOnly Today = new(2026, 10, 24);

    private readonly ContractTestKit _kit = new(Today);
    private readonly Meter _meter;
    private readonly MeterReading _september;
    private readonly MeterReading _october;
    private readonly Attachment _photo;

    public ReadingHistoryPhotoTests()
    {
        _meter = new Meter
        {
            PropertyId = _kit.Property.Id,
            Resource = MeterResource.ColdWater,
            Unit = MeterUnit.M3,
            Location = "Kitchen",
            InstalledOn = new DateOnly(2020, 1, 1),
        };
        var register = MeterRegister.CreateDefault(_meter.Id);
        _meter.Registers.Add(register);
        _september = new MeterReading { RegisterId = register.Id, Date = new DateOnly(2026, 9, 26), Value = 110m, Kind = ReadingKind.Regular };
        _october = new MeterReading { RegisterId = register.Id, Date = new DateOnly(2026, 10, 20), Value = 125m, Kind = ReadingKind.Regular };
        register.Readings.Add(_september);
        register.Readings.Add(_october);
        _kit.Meters.Stored.Add(_meter);

        // The October reading already has a photo.
        _photo = new Attachment { FileName = "old.jpg", ContentType = "image/jpeg", OwnerKind = AttachmentOwnerKind.MeterReading, OwnerId = _october.Id };
        _kit.Attachments.Added.Add((_photo, [1]));
        _october.PhotoAttachmentId = _photo.Id;
    }

    private async Task<ReadingHistoryViewModel> LoadAsync()
    {
        var vm = _kit.CreateHistory();
        await vm.LoadAsync(_meter.Id);
        return vm;
    }

    private MeterReading Stored(Guid id) =>
        _kit.Meters.ReadingsOf(_meter.Registers[0].Id).Single(r => r.Id == id);

    [Fact]
    public async Task Rows_with_a_photo_show_the_photo_icon()
    {
        var vm = await LoadAsync();

        var rows = vm.Groups[0].Rows;
        Assert.True(rows[0].HasPhoto);
        Assert.False(rows[1].HasPhoto);
    }

    [Fact]
    public async Task Tapping_the_photo_opens_it_in_the_system_viewer()
    {
        var vm = await LoadAsync();

        await vm.Groups[0].Rows[0].OpenPhotoCommand.ExecuteAsync(null);

        Assert.Equal([($"/attachments/{_photo.Id}.jpg", "image/jpeg")], _kit.Viewer.Opened);
    }

    [Fact]
    public async Task A_missing_photo_file_shows_a_message_instead_of_opening()
    {
        _kit.Attachments.MissingFiles.Add(_photo.Id);
        var vm = await LoadAsync();

        await vm.Groups[0].Rows[0].OpenPhotoCommand.ExecuteAsync(null);

        Assert.Empty(_kit.Viewer.Opened);
        Assert.Contains("PhotoMissing", Assert.Single(_kit.Dialogs.Alerts).Message);
    }

    [Fact]
    public async Task Edit_can_add_a_photo()
    {
        var vm = await LoadAsync();
        _kit.Media.Next = FakeMediaService.Photo("new.jpg", "image/jpeg", 4, 5);
        _kit.Dialogs.EditorScript = editor =>
        {
            var dialog = (ReadingEditorViewModel)editor;
            Assert.False(dialog.HasPhoto);
            dialog.ChoosePhotoCommand.Execute(null);
            Assert.True(dialog.HasPhoto);
            dialog.SaveCommand.Execute(null);
        };

        await vm.Groups[0].Rows[1].EditCommand.ExecuteAsync(null);

        var (attachment, bytes) = Assert.Single(_kit.Attachments.Added, a => a.Meta.OwnerId == _september.Id);
        Assert.Equal([4, 5], bytes);
        Assert.Equal(attachment.Id, Stored(_september.Id).PhotoAttachmentId);
        Assert.Empty(_kit.Attachments.Deleted);
        Assert.True(vm.Groups[0].Rows[1].HasPhoto);
    }

    [Fact]
    public async Task Edit_can_replace_the_photo_and_the_old_one_is_soft_deleted()
    {
        var vm = await LoadAsync();
        _kit.Media.Next = FakeMediaService.Photo("replacement.jpg");
        _kit.Dialogs.EditorScript = editor =>
        {
            var dialog = (ReadingEditorViewModel)editor;
            Assert.True(dialog.HasPhoto);
            dialog.TakePhotoCommand.Execute(null);
            dialog.SaveCommand.Execute(null);
        };

        await vm.Groups[0].Rows[0].EditCommand.ExecuteAsync(null);

        var added = Assert.Single(_kit.Attachments.Added, a => a.Meta.FileName == "replacement.jpg").Meta;
        Assert.Equal(_october.Id, added.OwnerId);
        Assert.Equal(added.Id, Stored(_october.Id).PhotoAttachmentId);
        Assert.Equal([_photo.Id], _kit.Attachments.Deleted);
        Assert.Equal(1, _kit.Media.CaptureCalls);
    }

    [Fact]
    public async Task Edit_can_remove_the_photo()
    {
        var vm = await LoadAsync();
        _kit.Dialogs.EditorScript = editor =>
        {
            var dialog = (ReadingEditorViewModel)editor;
            dialog.RemovePhotoCommand.Execute(null);
            Assert.False(dialog.HasPhoto);
            dialog.SaveCommand.Execute(null);
        };

        await vm.Groups[0].Rows[0].EditCommand.ExecuteAsync(null);

        Assert.Null(Stored(_october.Id).PhotoAttachmentId);
        Assert.Equal([_photo.Id], _kit.Attachments.Deleted);
        Assert.Single(_kit.Attachments.Added);
        Assert.False(vm.Groups[0].Rows[0].HasPhoto);
    }

    [Fact]
    public async Task Edit_without_touching_the_photo_keeps_it()
    {
        var vm = await LoadAsync();
        _kit.Dialogs.EditorScript = editor =>
        {
            var dialog = (ReadingEditorViewModel)editor;
            dialog.ValueText = "126";
            dialog.SaveCommand.Execute(null);
        };

        await vm.Groups[0].Rows[0].EditCommand.ExecuteAsync(null);

        Assert.Equal((126m, _photo.Id), (Stored(_october.Id).Value, Stored(_october.Id).PhotoAttachmentId));
        Assert.Empty(_kit.Attachments.Deleted);
    }

    [Fact]
    public async Task Cancelling_the_dialog_after_choosing_a_photo_stores_nothing()
    {
        var vm = await LoadAsync();
        _kit.Media.Next = FakeMediaService.Photo();
        _kit.Dialogs.EditorScript = editor =>
        {
            var dialog = (ReadingEditorViewModel)editor;
            dialog.ChoosePhotoCommand.Execute(null);
            dialog.CancelCommand.Execute(null);
        };

        await vm.Groups[0].Rows[1].EditCommand.ExecuteAsync(null);

        Assert.Single(_kit.Attachments.Added);
        Assert.Empty(_kit.Meters.UpdatedBatches);
    }

    [Fact]
    public async Task A_cancelled_picker_in_the_dialog_changes_nothing()
    {
        var vm = await LoadAsync();
        _kit.Media.Next = null;
        ReadingEditorViewModel? dialog = null;
        _kit.Dialogs.EditorScript = editor =>
        {
            dialog = (ReadingEditorViewModel)editor;
            dialog.ChoosePhotoCommand.Execute(null);
            dialog.SaveCommand.Execute(null);
        };

        await vm.Groups[0].Rows[1].EditCommand.ExecuteAsync(null);

        Assert.Null(dialog!.NewPhoto);
        Assert.Equal(string.Empty, dialog.PhotoError);
        Assert.Single(_kit.Attachments.Added);
        Assert.Null(Stored(_september.Id).PhotoAttachmentId);
    }

    [Fact]
    public async Task Denied_camera_permission_in_the_dialog_shows_an_error_and_keeps_the_dialog_working()
    {
        var vm = await LoadAsync();
        _kit.Media.Failure = new MediaPermissionDeniedException();
        ReadingEditorViewModel? dialog = null;
        _kit.Dialogs.EditorScript = editor =>
        {
            dialog = (ReadingEditorViewModel)editor;
            dialog.TakePhotoCommand.Execute(null);
            dialog.SaveCommand.Execute(null);
        };

        await vm.Groups[0].Rows[1].EditCommand.ExecuteAsync(null);

        Assert.Contains("PhotoPermissionDenied", dialog!.PhotoError);
        Assert.True(dialog.Saved);
        Assert.Null(Stored(_september.Id).PhotoAttachmentId);
    }

    [Fact]
    public async Task A_file_the_store_rejects_keeps_the_reading_unchanged()
    {
        var vm = await LoadAsync();
        _kit.Media.Next = FakeMediaService.Photo();
        _kit.Attachments.Rejection = ValidationResult.Fail(new ValidationError(ErrorCodes.AttachmentEmpty, "SizeBytes"));
        _kit.Dialogs.EditorScript = editor =>
        {
            var dialog = (ReadingEditorViewModel)editor;
            dialog.ChoosePhotoCommand.Execute(null);
            dialog.ValueText = "111";
            dialog.SaveCommand.Execute(null);
        };

        await vm.Groups[0].Rows[1].EditCommand.ExecuteAsync(null);

        Assert.Contains("Error_attachment_empty", Assert.Single(_kit.Dialogs.Alerts).Message);
        Assert.Empty(_kit.Meters.UpdatedBatches);
        Assert.Equal(110m, Stored(_september.Id).Value);
    }
}
