using System;
using System.IO;
using System.Linq;
using System.Text;

namespace ModManagerEnhancements.Core;

// A confirmed edit applies to the exact file contents shown when the dialog opened.
internal sealed class CompatibilityEdit
{
    private readonly string path;
    private readonly byte[] original, replacement;
    internal bool IsRestore { get; }

    private CompatibilityEdit(string path, byte[] original, byte[] replacement, bool restore)
    { this.path = path; this.original = original; this.replacement = replacement; IsRestore = restore; }

    internal static CompatibilityEdit Prepare(string path, string version)
    {
        if (!CompatibilityDocument.ValidVersion(version))
            throw new ArgumentException("The current game version is invalid.", nameof(version));
        return Create(path, version);
    }
    internal static CompatibilityEdit PrepareRestore(string path) => Create(path, null);
    internal static CompatibilityOverrideState Inspect(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        var (encoding, offset) = EncodingOf(bytes);
        return new CompatibilityDocument(encoding.GetString(bytes, offset, bytes.Length - offset)).State();
    }
    private static CompatibilityEdit Create(string path, string? version)
    {
        byte[] bytes = File.ReadAllBytes(path);
        var (encoding, offset) = EncodingOf(bytes);
        var document = new CompatibilityDocument(encoding.GetString(bytes, offset, bytes.Length - offset));
        byte[] edited = encoding.GetBytes(version == null ? document.Restore() : document.Override(version));
        return new CompatibilityEdit(path, bytes, bytes.Take(offset).Concat(edited).ToArray(), version == null);
    }

    internal void Commit()
    {
        if (!File.ReadAllBytes(path).SequenceEqual(original))
            throw new IOException("The mod's _info.ini changed while confirmation was open. Please try again.");
        if (original.SequenceEqual(replacement)) return;
        string temporary = path + ".mme-" + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(replacement, 0, replacement.Length); stream.Flush(true); }
            File.Replace(temporary, path, null);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static (Encoding Encoding, int Offset) EncodingOf(byte[] bytes)
    {
        bool Starts(params byte[] prefix) => bytes.Length >= prefix.Length && bytes.Take(prefix.Length).SequenceEqual(prefix);
        if (Starts(255, 254, 0, 0)) return (new UTF32Encoding(false, false, true), 4);
        if (Starts(0, 0, 254, 255)) return (new UTF32Encoding(true, false, true), 4);
        if (Starts(255, 254)) return (new UnicodeEncoding(false, false, true), 2);
        if (Starts(254, 255)) return (new UnicodeEncoding(true, false, true), 2);
        // Byte-preserving ASCII edits also retain non-ASCII UTF-8 or legacy text verbatim.
        return (Encoding.GetEncoding(28591), Starts(239, 187, 191) ? 3 : 0);
    }
}
