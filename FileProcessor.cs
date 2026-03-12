using System.Text;

namespace Utf8Converter;

public enum LogTag { AlreadyUtf8, Converted, Skipped, Error }

public record LogEntry(string Text, System.Drawing.Color Color, LogTag Tag = LogTag.AlreadyUtf8);

/// <summary>
/// Processes a single subtitle file: detects encoding, converts to UTF-8 if needed.
/// </summary>
public static class FileProcessor
{
    // UTF-8 without BOM for writing
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);
    // UTF-8 with BOM — same code page as without BOM in .NET
    private static readonly int Utf8CodePage = Encoding.UTF8.CodePage;

    public static LogEntry Process(string filePath)
    {
        var info = new FileInfo(filePath);

        // Skip zero-byte or very large "text" files (> 50 MB) — likely not subtitles
        if (info.Length == 0)
            return Skip(filePath, "prazna datoteka");
        if (info.Length > 50 * 1024 * 1024)
            return Skip(filePath, "datoteka prevelika (>50 MB)");

        // Detect encoding
        Encoding? detected;
        try
        {
            detected = EncodingDetector.Detect(filePath);
        }
        catch
        {
            detected = null;
        }

        if (detected == null)
            return Skip(filePath, "nije moguće odrediti encoding");

        bool isBomUtf8 = detected is UTF8Encoding && detected.GetPreamble().Length > 0;

        // Already UTF-8 (with or without BOM) — nothing to do
        if (detected.CodePage == Utf8CodePage)
            return AlreadyOk(filePath, isBomUtf8 ? "UTF-8 (BOM)" : "UTF-8");

        // Read with detected encoding and re-save as UTF-8 without BOM
        string content;
        try
        {
            content = File.ReadAllText(filePath, detected);
        }
        catch (DecoderFallbackException)
        {
            return Skip(filePath, $"decode greška s {detected.EncodingName}");
        }

        // Safety: if content appears binary (many null bytes) skip it
        int nulls = content.Count(c => c == '\0');
        if (nulls > content.Length / 20) // >5 % null chars → binary
            return Skip(filePath, "izgleda kao binarna datoteka");

        File.WriteAllText(filePath, content, Utf8NoBom);

        return Converted(filePath, detected.EncodingName);
    }

    // ── Log entry factories ───────────────────────────────────────────────

    private static LogEntry AlreadyOk(string path, string encName) =>
        new(
            $"[OK]          {Fmt(path)}  ({encName})",
            System.Drawing.Color.FromArgb(100, 200, 100),
            LogTag.AlreadyUtf8);

    private static LogEntry Converted(string path, string fromEnc) =>
        new(
            $"[KONVERTIRAN] {Fmt(path)}  ({fromEnc} → UTF-8)",
            System.Drawing.Color.FromArgb(80, 160, 255),
            LogTag.Converted);

    private static LogEntry Skip(string path, string reason) =>
        new(
            $"[PRESKOČEN]   {Fmt(path)}  ({reason})",
            System.Drawing.Color.FromArgb(140, 140, 140),
            LogTag.Skipped);

    private static string Fmt(string path) =>
        path.Length > 75 ? "…" + path[^72..] : path;
}

/// <summary>
/// Recursively finds subtitle/text files under a root directory.
/// </summary>
public static class FileScanner
{
    public static string[] FindSubtitleFiles(string rootPath)
    {
        var results = new List<string>();
        var extensions = new HashSet<string>(
            EncodingDetector.SubtitleExtensions,
            StringComparer.OrdinalIgnoreCase);

        foreach (var file in Directory.EnumerateFiles(
                     rootPath, "*", SearchOption.AllDirectories))
        {
            if (extensions.Contains(Path.GetExtension(file)))
                results.Add(file);
        }

        results.Sort(StringComparer.OrdinalIgnoreCase);
        return [.. results];
    }
}
