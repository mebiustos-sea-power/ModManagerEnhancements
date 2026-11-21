using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ModManagerEnhancements.Core;

internal enum CompatibilityOverrideState { None, Modified, Unreadable }

// Edits only ExactVersion and MME's saved original data; other INI data stays in place.
internal sealed class CompatibilityDocument
{
    private const string Section = "ModManagerEnhancements";
    private const string OriginalKey = "OriginalExactVersionData";
    private readonly List<(string Text, string End)> lines;
    private readonly string newline;

    internal CompatibilityDocument(string contents)
    {
        lines = Regex.Matches(contents, @"([^\r\n]*)(\r\n|\r|\n|$)").Cast<Match>()
            .Where(match => match.Length > 0).Select(match => (match.Groups[1].Value, match.Groups[2].Value)).ToList();
        newline = lines.Select(line => line.End).FirstOrDefault(end => end.Length > 0) ?? Environment.NewLine;
    }

    internal static bool ValidVersion(string version) =>
        Regex.IsMatch(version, @"\A[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?\z");

    internal CompatibilityOverrideState State()
    {
        return ReadRecord() == null ? CompatibilityOverrideState.None : CompatibilityOverrideState.Modified;
    }

    internal string Override(string version)
    {
        int header = FirstCompatibilityHeader();
        if (header < 0) throw new InvalidDataException("The mod's _info.ini has no [Compatibility] section.");
        var record = ReadRecord();
        var entries = ExactEntries();
        var marker = MarkerRange();
        record ??= new Record
        {
            CreatedSection = marker == null,
            HeaderEnd = lines[header].End,
            MarkerHeaderEnd = marker == null ? "" : lines[marker.Value.Start].End,
            Originals = entries.Select(entry => new Original(entry.Section, lines[entry.Index].Text)).ToArray()
        };
        foreach (var entry in entries)
            lines[entry.Index] = (RewriteLine(lines[entry.Index].Text, version), lines[entry.Index].End);
        if (entries.Count == 0)
        {
            var originalHeader = lines[header];
            lines[header] = (originalHeader.Text, originalHeader.End.Length > 0 ? originalHeader.End : newline);
            lines.Insert(header + 1, ("ExactVersion=" + version, originalHeader.End));
        }
        WriteRecord(record);
        return Contents();
    }

    internal string Restore()
    {
        var record = ReadRecord() ?? throw new InvalidDataException("This mod has no saved MME compatibility setting.");
        var originals = record.Originals.GroupBy(original => original.Section)
            .ToDictionary(group => group.Key, group => group.ToArray());
        int sectionCount = CompatibilityHeaders().Count;
        for (int section = 0; section < sectionCount; section++)
        {
            var saved = originals.TryGetValue(section, out var group) ? group : Array.Empty<Original>();
            var current = ExactEntries().Where(entry => entry.Section == section).ToArray();
            for (int i = current.Length - 1; i >= 0; i--)
            {
                int at = current[i].Index;
                if (i < saved.Length)
                    lines[at] = (RewriteLine(lines[at].Text, Value(saved[i].Text)), lines[at].End);
                else RemoveExactLine(at);
            }
            for (int i = current.Length; i < saved.Length; i++) InsertExactLine(section, saved[i].Text);
        }
        foreach (var missing in originals.Where(group => group.Key >= sectionCount).OrderBy(group => group.Key))
        {
            EndLastLine();
            lines.Add(("[Compatibility]", newline));
            int section = CompatibilityHeaders().Count - 1;
            foreach (var original in missing.Value) InsertExactLine(section, original.Text);
        }
        int header = FirstCompatibilityHeader();
        if (record.Originals.Length == 0 && record.HeaderEnd.Length == 0 && header >= 0 && header == lines.Count - 1)
            lines[header] = (lines[header].Text, "");
        RemoveRecordKeys();
        var marker = MarkerRange()!.Value;
        bool hasOtherData = lines.Skip(marker.Start + 1).Take(marker.End - marker.Start - 1)
            .Any(line => Parsed(line.Text).Length > 0);
        if (record.CreatedSection && !hasOtherData) lines.RemoveAt(marker.Start);
        else if (!record.CreatedSection && record.MarkerHeaderEnd.Length == 0 && marker.Start == lines.Count - 1)
            lines[marker.Start] = (lines[marker.Start].Text, "");
        return Contents();
    }

    private void RemoveExactLine(int at)
    {
        var comment = Regex.Match(lines[at].Text, @"//|;|#");
        if (comment.Success)
            lines[at] = (Regex.Match(lines[at].Text, @"\A[ \t]*").Value + lines[at].Text.Substring(comment.Index), lines[at].End);
        else lines.RemoveAt(at);
    }
    private void InsertExactLine(int section, string text)
    {
        var entries = ExactEntries().Where(entry => entry.Section == section).ToArray();
        int at = entries.Length == 0 ? CompatibilityHeaders()[section] + 1 : entries.Last().Index + 1;
        if (at > 0 && lines[at - 1].End.Length == 0) lines[at - 1] = (lines[at - 1].Text, newline);
        lines.Insert(at, (text, newline));
    }
    private void EndLastLine()
    { if (lines.Count > 0 && lines[lines.Count - 1].End.Length == 0) lines[lines.Count - 1] = (lines[lines.Count - 1].Text, newline); }

    private static string Parsed(string text) => Regex.Split(text, @";|//|#")[0].Trim();
    private static string? Header(string text)
    {
        var match = Regex.Match(Parsed(text), @"\A\[(.+)\]");
        return match.Success ? match.Groups[1].Value : null;
    }
    private static string? Key(string text)
    {
        string parsed = Parsed(text);
        int equals = parsed.IndexOf('=');
        return equals < 0 ? null : parsed.Substring(0, equals).Trim();
    }
    private static string Value(string text)
    { string parsed = Parsed(text); return parsed.Substring(parsed.IndexOf('=') + 1).Trim(); }
    private static bool Same(string? left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    private static bool RecordKey(string text) => Same(Key(text), OriginalKey);
    private string Contents() => string.Concat(lines.Select(line => line.Text + line.End));

    private int FirstCompatibilityHeader() => lines.FindIndex(line => Same(Header(line.Text), "Compatibility"));
    private List<int> CompatibilityHeaders() => lines.Select((line, index) => (line, index))
        .Where(item => Same(Header(item.line.Text), "Compatibility")).Select(item => item.index).ToList();
    private List<(int Index, int Section)> ExactEntries()
    {
        var entries = new List<(int, int)>();
        bool inCompatibility = false;
        int section = -1;
        for (int i = 0; i < lines.Count; i++)
        {
            string? header = Header(lines[i].Text);
            if (header != null)
            {
                inCompatibility = Same(header, "Compatibility");
                if (inCompatibility) section++;
            }
            else if (inCompatibility && Same(Key(lines[i].Text), "ExactVersion")) entries.Add((i, section));
        }
        return entries;
    }

    private (int Start, int End)? MarkerRange()
    {
        int start = -1, end = lines.Count;
        for (int i = 0; i < lines.Count; i++)
        {
            string? header = Header(lines[i].Text);
            if (header == null) continue;
            if (start >= 0 && end == lines.Count) end = i;
            if (!Same(header, Section)) continue;
            if (start >= 0) throw new InvalidDataException("The mod has duplicate MME metadata sections.");
            start = i;
        }
        return start < 0 ? null : (start, end);
    }

    private Record? ReadRecord()
    {
        var range = MarkerRange();
        if (range == null) return null;
        string? data = null;
        for (int i = range.Value.Start + 1; i < range.Value.End; i++)
        {
            if (!RecordKey(lines[i].Text)) continue;
            if (data != null) throw new InvalidDataException("The mod has duplicate MME record keys.");
            data = Value(lines[i].Text);
        }
        if (data == null) return null;
        try
        {
            using var stream = new MemoryStream(Convert.FromBase64String(data.Replace('-', '+').Replace('_', '/')));
            using var reader = new BinaryReader(stream, Encoding.UTF8);
            if (reader.ReadInt32() != 1) throw new InvalidDataException("Unknown MME record format.");
            var record = new Record
            {
                CreatedSection = reader.ReadBoolean(),
                HeaderEnd = reader.ReadString(), MarkerHeaderEnd = reader.ReadString()
            };
            bool LineEnd(string end) => end is "" or "\n" or "\r" or "\r\n";
            if (!LineEnd(record.HeaderEnd) || !LineEnd(record.MarkerHeaderEnd)) throw new InvalidDataException("Invalid saved line ending.");
            int count = reader.ReadInt32();
            if (count < 0 || count > stream.Length / 5) throw new InvalidDataException("Invalid saved entry count.");
            var originals = new List<Original>();
            for (int i = 0; i < count; i++)
            {
                int section = reader.ReadInt32();
                string text = reader.ReadString();
                if (section < 0 || text.IndexOfAny(new[] { '\r', '\n' }) >= 0 || !Same(Key(text), "ExactVersion"))
                    throw new InvalidDataException("Invalid saved compatibility entry.");
                originals.Add(new Original(section, text));
            }
            if (stream.Position != stream.Length) throw new InvalidDataException("Unexpected data in the MME record.");
            record.Originals = originals.ToArray();
            return record;
        }
        catch (Exception error) when (error is FormatException || error is IOException || error is ArgumentException)
        { throw new InvalidDataException("The saved MME compatibility setting could not be read.", error); }
    }

    private void WriteRecord(Record record)
    {
        RemoveRecordKeys();
        string encoded;
        using (var stream = new MemoryStream())
        {
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(1); writer.Write(record.CreatedSection);
                writer.Write(record.HeaderEnd); writer.Write(record.MarkerHeaderEnd);
                writer.Write(record.Originals.Length);
                foreach (var original in record.Originals) { writer.Write(original.Section); writer.Write(original.Text); }
            }
            encoded = Convert.ToBase64String(stream.ToArray()).Replace('+', '-').Replace('/', '_');
        }
        var added = new[] { (OriginalKey + "=" + encoded, newline) };
        var range = MarkerRange();
        if (range == null)
        {
            int at = FirstCompatibilityHeader();
            lines.Insert(at, ("[" + Section + "]", newline));
            lines.InsertRange(at + 1, added);
        }
        else
        {
            int at = range.Value.Start;
            if (lines[at].End.Length == 0) lines[at] = (lines[at].Text, newline);
            lines.InsertRange(at + 1, added);
        }
    }

    private void RemoveRecordKeys()
    {
        var range = MarkerRange();
        if (range == null) return;
        for (int i = range.Value.End - 1; i > range.Value.Start; i--)
            if (RecordKey(lines[i].Text)) lines.RemoveAt(i);
    }

    private static string RewriteLine(string text, string version)
    {
        int equals = text.IndexOf('=');
        string value = text.Substring(equals + 1);
        string leading = Regex.Match(value, @"\A[ \t]*").Value;
        var comment = Regex.Match(value, @"//|;|#");
        int end = comment.Success ? comment.Index : value.Length;
        while (end > leading.Length && char.IsWhiteSpace(value[end - 1])) end--;
        return text.Substring(0, equals + 1) + leading + version + value.Substring(end);
    }

    private sealed class Record
    {
        internal string HeaderEnd = "", MarkerHeaderEnd = "";
        internal bool CreatedSection;
        internal Original[] Originals = Array.Empty<Original>();
    }
    private readonly struct Original
    {
        internal int Section { get; }
        internal string Text { get; }
        internal Original(int section, string text) { Section = section; Text = text; }
    }
}
