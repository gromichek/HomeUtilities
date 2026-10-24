using HomeUtilities.Core.Attachments;
using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Tests.Attachments;

public class AttachmentValidatorTests
{
    private static readonly string Hash = new('a', 64);

    private static Attachment Valid() => new()
    {
        FileName = "dial.jpg",
        ContentType = AttachmentContentTypes.Jpeg,
        SizeBytes = 1234,
        Sha256 = Hash,
        OwnerKind = AttachmentOwnerKind.MeterReading,
        OwnerId = Guid.NewGuid(),
    };

    private static string SingleCode(Attachment attachment) => Assert.Single(AttachmentValidator.Validate(attachment).Errors).Code;

    [Fact]
    public void Valid_attachment_passes()
    {
        Assert.True(AttachmentValidator.Validate(Valid()).IsValid);
    }

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("image/heic")]
    [InlineData("application/pdf")]
    [InlineData("IMAGE/JPEG")]
    public void Allowed_content_types_pass(string contentType)
    {
        var attachment = Valid();
        attachment.ContentType = contentType;
        Assert.True(AttachmentValidator.Validate(attachment).IsValid);
    }

    [Theory]
    [InlineData("image/gif")]
    [InlineData("text/plain")]
    [InlineData("application/x-msdownload")]
    public void Other_content_types_fail(string contentType)
    {
        var attachment = Valid();
        attachment.ContentType = contentType;
        Assert.Equal(ErrorCodes.AttachmentContentTypeNotAllowed, SingleCode(attachment));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Blank_content_type_fails(string contentType)
    {
        var attachment = Valid();
        attachment.ContentType = contentType;
        Assert.Equal(ErrorCodes.AttachmentContentTypeRequired, SingleCode(attachment));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_file_name_fails(string name)
    {
        var attachment = Valid();
        attachment.FileName = name;
        var error = Assert.Single(AttachmentValidator.Validate(attachment).Errors);
        Assert.Equal((ErrorCodes.AttachmentFileNameRequired, "FileName"), (error.Code, error.Field));
    }

    [Fact]
    public void File_name_is_limited_to_200_characters()
    {
        var attachment = Valid();
        attachment.FileName = new string('n', 200);
        Assert.True(AttachmentValidator.Validate(attachment).IsValid);

        attachment.FileName = new string('n', 201);
        Assert.Equal(ErrorCodes.AttachmentFileNameTooLong, SingleCode(attachment));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Empty_file_fails(long size)
    {
        var attachment = Valid();
        attachment.SizeBytes = size;
        Assert.Equal(ErrorCodes.AttachmentEmpty, SingleCode(attachment));
    }

    [Fact]
    public void Size_is_limited_to_20_MB()
    {
        var attachment = Valid();
        attachment.SizeBytes = 20L * 1024 * 1024;
        Assert.True(AttachmentValidator.Validate(attachment).IsValid);

        attachment.SizeBytes = 20L * 1024 * 1024 + 1;
        var error = Assert.Single(AttachmentValidator.Validate(attachment).Errors);
        Assert.Equal(ErrorCodes.AttachmentTooLarge, error.Code);
        Assert.Equal(20L, error.Args![0]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public void Sha256_must_be_64_hex_characters(string hash)
    {
        var attachment = Valid();
        attachment.Sha256 = hash;
        Assert.Equal(ErrorCodes.AttachmentHashInvalid, SingleCode(attachment));
    }

    [Fact]
    public void Notes_are_limited()
    {
        var attachment = Valid();
        attachment.Notes = new string('x', 2001);
        Assert.Equal(ErrorCodes.NotesTooLong, SingleCode(attachment));
    }

    [Theory]
    [InlineData("a.JPG", "image/jpeg")]
    [InlineData("scan.jpeg", "image/jpeg")]
    [InlineData("x.png", "image/png")]
    [InlineData("x.HEIC", "image/heic")]
    [InlineData("bill.pdf", "application/pdf")]
    public void Content_type_is_derived_from_the_extension(string fileName, string expected)
    {
        Assert.Equal(expected, AttachmentContentTypes.FromFileName(fileName));
    }

    [Theory]
    [InlineData("x.exe")]
    [InlineData("noext")]
    [InlineData("")]
    public void Unknown_extension_has_no_content_type(string fileName)
    {
        Assert.Null(AttachmentContentTypes.FromFileName(fileName));
    }

    [Theory]
    [InlineData("image/jpeg", ".jpg")]
    [InlineData("image/png", ".png")]
    [InlineData("image/heic", ".heic")]
    [InlineData("application/pdf", ".pdf")]
    public void Stored_extension_follows_the_content_type(string contentType, string extension)
    {
        Assert.Equal(extension, AttachmentContentTypes.ExtensionFor(contentType));
    }
}
