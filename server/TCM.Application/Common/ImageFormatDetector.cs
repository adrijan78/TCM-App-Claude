namespace TCM.Application.Common;

/// <summary>
/// Works out what an uploaded file actually is by reading its magic numbers.
/// </summary>
/// <remarks>
/// The declared <c>Content-Type</c> and the file extension are both attacker-controlled, so
/// neither is consulted. A renamed executable must not become a stored "image" that some other
/// client is later persuaded to run.
/// </remarks>
public static class ImageFormatDetector
{
    /// <summary>Returns the real content type, or null when the bytes are not a supported image.</summary>
    public static string? Detect(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 12) return null;

        // JPEG: FF D8 FF
        if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return "image/jpeg";

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        // Spelled out as a ReadOnlySpan<byte> — a bare collection expression here would be
        // inferred as int and fail to match the span's element type.
        ReadOnlySpan<byte> pngMagic = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (bytes[..8].SequenceEqual(pngMagic))
            return "image/png";

        // GIF: "GIF87a" or "GIF89a"
        if (bytes[..6].SequenceEqual("GIF87a"u8) || bytes[..6].SequenceEqual("GIF89a"u8))
            return "image/gif";

        // WebP: "RIFF" .... "WEBP"
        if (bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8))
            return "image/webp";

        return null;
    }
}
