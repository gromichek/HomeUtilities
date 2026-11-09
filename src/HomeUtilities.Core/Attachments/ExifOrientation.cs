namespace HomeUtilities.Core.Attachments;

/// <summary>
/// Reads the EXIF orientation (tag <c>0x0112</c>) of a JPEG without any package (T-042a): JPEG APP1 "Exif" segment, TIFF
/// header in either byte order, IFD0. Never throws — anything unreadable (no APP1, no tag, truncated data) is orientation 1
/// ("as stored"), so the caller can simply rotate by <see cref="ImageSizing.RotationFor"/>.
/// </summary>
public static class ExifOrientation
{
    /// <summary>The value meaning "upright as stored".</summary>
    public const int Normal = 1;

    private const ushort OrientationTag = 0x0112;

    /// <summary>The EXIF orientation (1–8) of the JPEG in <paramref name="jpeg"/>, or 1 when there is none.</summary>
    public static int Read(ReadOnlySpan<byte> jpeg)
    {
        try
        {
            return ReadCore(jpeg);
        }
        catch (Exception ex) when (ex is ArgumentException or IndexOutOfRangeException)
        {
            return Normal;
        }
    }

    /// <summary>Whether an image with this orientation has to be rotated to be stored upright without EXIF.</summary>
    public static bool NeedsRotation(int orientation) => ImageSizing.RotationFor(orientation) != 0;

    private static int ReadCore(ReadOnlySpan<byte> data)
    {
        if (data.Length < 4 || data[0] != 0xFF || data[1] != 0xD8)
        {
            return Normal;
        }

        var pos = 2;
        while (pos + 4 <= data.Length)
        {
            if (data[pos] != 0xFF)
            {
                return Normal;
            }

            var marker = data[pos + 1];
            if (marker == 0xFF)
            {
                pos++; // fill byte
                continue;
            }

            if (marker is 0xD8 or 0x01 || marker is >= 0xD0 and <= 0xD7)
            {
                pos += 2; // markers without a length
                continue;
            }

            if (marker is 0xDA or 0xD9)
            {
                return Normal; // start of scan / end of image: no EXIF before it
            }

            var length = (data[pos + 2] << 8) | data[pos + 3];
            if (length < 2)
            {
                return Normal;
            }

            var segmentStart = pos + 4;
            var segmentEnd = Math.Min(pos + 2 + length, data.Length);
            if (marker == 0xE1 && segmentEnd - segmentStart >= 6 && IsExifHeader(data.Slice(segmentStart, 6)))
            {
                return ReadTiff(data[(segmentStart + 6)..segmentEnd]);
            }

            pos += 2 + length;
        }

        return Normal;
    }

    private static bool IsExifHeader(ReadOnlySpan<byte> h) =>
        h[0] == (byte)'E' && h[1] == (byte)'x' && h[2] == (byte)'i' && h[3] == (byte)'f' && h[4] == 0 && h[5] == 0;

    private static int ReadTiff(ReadOnlySpan<byte> tiff)
    {
        if (tiff.Length < 8)
        {
            return Normal;
        }

        bool littleEndian;
        if (tiff[0] == 'I' && tiff[1] == 'I')
        {
            littleEndian = true;
        }
        else if (tiff[0] == 'M' && tiff[1] == 'M')
        {
            littleEndian = false;
        }
        else
        {
            return Normal;
        }

        if (U16(tiff, 2, littleEndian) != 42)
        {
            return Normal;
        }

        var ifd = (long)U32(tiff, 4, littleEndian);
        if (ifd < 8 || ifd + 2 > tiff.Length)
        {
            return Normal;
        }

        var count = U16(tiff, (int)ifd, littleEndian);
        for (var i = 0; i < count; i++)
        {
            var entry = ifd + 2 + (i * 12L);
            if (entry + 12 > tiff.Length)
            {
                return Normal;
            }

            if (U16(tiff, (int)entry, littleEndian) != OrientationTag)
            {
                continue;
            }

            var type = U16(tiff, (int)entry + 2, littleEndian);
            var value = type == 3 ? U16(tiff, (int)entry + 8, littleEndian) : 0; // SHORT
            return value is >= 1 and <= 8 ? value : Normal;
        }

        return Normal;
    }

    private static int U16(ReadOnlySpan<byte> d, int i, bool le) => le ? d[i] | (d[i + 1] << 8) : (d[i] << 8) | d[i + 1];

    private static uint U32(ReadOnlySpan<byte> d, int i, bool le) => le
        ? (uint)(d[i] | (d[i + 1] << 8) | (d[i + 2] << 16) | (d[i + 3] << 24))
        : (uint)((d[i] << 24) | (d[i + 1] << 16) | (d[i + 2] << 8) | d[i + 3]);
}
