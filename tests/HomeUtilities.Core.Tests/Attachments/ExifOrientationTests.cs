using HomeUtilities.Core.Attachments;

namespace HomeUtilities.Core.Tests.Attachments;

public class ExifOrientationTests
{
    // Synthetic JPEG: SOI, optional APP0, APP1 "Exif" with a TIFF block holding the given IFD0 entries, then SOS.
    private static byte[] Jpeg(bool littleEndian, int? orientation, bool otherTagFirst = false, bool withApp0 = false)
    {
        var tiff = new List<byte>();
        void U16(int v) { if (littleEndian) { tiff.Add((byte)v); tiff.Add((byte)(v >> 8)); } else { tiff.Add((byte)(v >> 8)); tiff.Add((byte)v); } }
        void U32(int v) { if (littleEndian) { U16(v & 0xFFFF); U16(v >> 16); } else { U16(v >> 16); U16(v & 0xFFFF); } }

        tiff.AddRange(littleEndian ? "II"u8.ToArray() : "MM"u8.ToArray());
        U16(42);
        U32(8);
        var entries = new List<(int Tag, int Value)>();
        if (otherTagFirst)
        {
            entries.Add((0x010F, 0)); // Make
        }

        if (orientation is { } o)
        {
            entries.Add((0x0112, o));
        }

        U16(entries.Count);
        foreach (var (tag, value) in entries)
        {
            U16(tag);
            U16(3); // SHORT
            U32(1);
            U16(value);
            U16(0);
        }

        U32(0); // no next IFD

        var app1 = new List<byte> { 0xFF, 0xE1 };
        var length = 2 + 6 + tiff.Count;
        app1.Add((byte)(length >> 8));
        app1.Add((byte)length);
        app1.AddRange("Exif\0\0"u8.ToArray());
        app1.AddRange(tiff);

        var jpeg = new List<byte> { 0xFF, 0xD8 };
        if (withApp0)
        {
            jpeg.AddRange(new byte[] { 0xFF, 0xE0, 0x00, 0x04, 0x00, 0x00 });
        }

        jpeg.AddRange(app1);
        jpeg.AddRange(new byte[] { 0xFF, 0xDA, 0x00, 0x02, 0x00 });
        return [.. jpeg];
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(true, 3)]
    [InlineData(true, 6)]
    [InlineData(true, 8)]
    [InlineData(false, 1)]
    [InlineData(false, 3)]
    [InlineData(false, 6)]
    [InlineData(false, 8)]
    public void Read_ReturnsTheOrientationInBothByteOrders(bool littleEndian, int orientation) =>
        Assert.Equal(orientation, ExifOrientation.Read(Jpeg(littleEndian, orientation)));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Read_FindsTheTagAfterAnotherEntryAndAnApp0Segment(bool littleEndian) =>
        Assert.Equal(6, ExifOrientation.Read(Jpeg(littleEndian, 6, otherTagFirst: true, withApp0: true)));

    [Fact]
    public void Read_NoApp1_IsNormal() =>
        Assert.Equal(1, ExifOrientation.Read(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x04, 0x00, 0x00, 0xFF, 0xDA, 0x00, 0x02, 0x00 }));

    [Fact]
    public void Read_App1WithoutOrientation_IsNormal() => Assert.Equal(1, ExifOrientation.Read(Jpeg(true, null, otherTagFirst: true)));

    [Fact]
    public void Read_OutOfRangeValue_IsNormal() => Assert.Equal(1, ExifOrientation.Read(Jpeg(true, 9)));

    [Fact]
    public void Read_NotAJpeg_IsNormal() => Assert.Equal(1, ExifOrientation.Read("not a jpeg at all"u8));

    [Fact]
    public void Read_Empty_IsNormal() => Assert.Equal(1, ExifOrientation.Read([]));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Read_TruncatedAtEveryLength_NeverThrows(bool littleEndian)
    {
        var full = Jpeg(littleEndian, 6);
        for (var length = 0; length < full.Length; length++)
        {
            var result = ExifOrientation.Read(full.AsSpan(0, length));
            Assert.True(result is 1 or 6);
        }

        Assert.Equal(1, ExifOrientation.Read(full.AsSpan(0, 20)));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(3, true)]
    [InlineData(6, true)]
    [InlineData(8, true)]
    public void NeedsRotation_OnlyForRotatedOrientations(int orientation, bool expected) =>
        Assert.Equal(expected, ExifOrientation.NeedsRotation(orientation));
}
