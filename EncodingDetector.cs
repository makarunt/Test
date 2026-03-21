using System.Text;
using Ude;

namespace Utf8Converter;

/// <summary>
/// Detects the character encoding of a text file using BOM inspection
/// and the Mozilla Universal Charset Detector (Ude).
/// </summary>
public static class EncodingDetector
{
    // Subtitle and text file extensions the app cares about
    public static readonly string[] SubtitleExtensions =
        [".srt", ".sub", ".ass", ".ssa", ".vtt", ".txt", ".sbv", ".smi", ".mpl"];

    /// <summary>
    /// Attempts to detect the encoding of the given file.
    /// Returns null if detection fails conclusively.
    /// </summary>
    public static Encoding? Detect(string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        // 1. Check BOM first — fastest and most reliable
        var bomEncoding = DetectByBom(fs);
        if (bomEncoding != null)
            return bomEncoding;

        fs.Seek(0, SeekOrigin.Begin);

        // 2. Try Ude (Mozilla Universal Charset Detector)
        var udeEncoding = DetectByUde(fs);
        if (udeEncoding != null)
            return udeEncoding;

        fs.Seek(0, SeekOrigin.Begin);

        // 3. If Ude gave up, check whether bytes are valid UTF-8
        if (IsValidUtf8(fs))
            return new UTF8Encoding(false);

        // 4. Last resort: assume Windows-1250 (common for Central/SE European subtitles)
        return Encoding.GetEncoding(1250);
    }

    private static Encoding? DetectByBom(Stream stream)
    {
        var bom = new byte[4];
        int read = stream.Read(bom, 0, 4);
        if (read < 2) return null;

        // UTF-32 LE  (must be checked before UTF-16 LE)
        if (read >= 4 && bom[0] == 0xFF && bom[1] == 0xFE && bom[2] == 0x00 && bom[3] == 0x00)
            return Encoding.UTF32;

        // UTF-32 BE
        if (read >= 4 && bom[0] == 0x00 && bom[1] == 0x00 && bom[2] == 0xFE && bom[3] == 0xFF)
            return new UTF32Encoding(bigEndian: true, byteOrderMark: true);

        // UTF-8 BOM
        if (read >= 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

        // UTF-16 LE
        if (bom[0] == 0xFF && bom[1] == 0xFE)
            return Encoding.Unicode;

        // UTF-16 BE
        if (bom[0] == 0xFE && bom[1] == 0xFF)
            return Encoding.BigEndianUnicode;

        return null;
    }

    // Encodingi koje Ude često brkaju s Windows-1250 za slavenske jezike.
    // Kad Ude vrati jedan od ovih, ignoriramo ga i padamo na Windows-1250 fallback.
    private static readonly HashSet<string> AmbiguousLatinEncodings =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "windows-1252", "iso-8859-1", "iso-8859-2", "latin-1", "x-iso-8859-1"
        };

    private static Encoding? DetectByUde(Stream stream)
    {
        var detector = new CharsetDetector();
        detector.Feed(stream);
        detector.DataEnd();

        // Zahtijevamo visoku pouzdanost (0.85) da izbjegnemo krive detekcije
        if (detector.Charset == null || detector.Confidence < 0.85f)
            return null;

        // Western European encodinge ignoriramo — često se brkaju s Windows-1250
        // za tekst s dijakritičkim znakovima (č, š, ž, ć, đ)
        if (AmbiguousLatinEncodings.Contains(detector.Charset))
            return null;

        try
        {
            return Encoding.GetEncoding(detector.Charset);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static bool IsValidUtf8(Stream stream)
    {
        int b;
        while ((b = stream.ReadByte()) != -1)
        {
            int extra;
            if (b <= 0x7F)       extra = 0;          // ASCII
            else if (b >> 5 == 0b110)  extra = 1;   // 2-byte
            else if (b >> 4 == 0b1110) extra = 2;   // 3-byte
            else if (b >> 3 == 0b11110) extra = 3;  // 4-byte
            else return false;                        // invalid lead byte

            for (int i = 0; i < extra; i++)
            {
                int c = stream.ReadByte();
                if (c == -1 || (c >> 6) != 0b10) return false; // invalid continuation
            }
        }
        return true;
    }
}
