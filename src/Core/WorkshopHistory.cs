using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ModManagerEnhancements.Core;

internal sealed class WorkshopHistory
{
    private readonly string path;
    private readonly Dictionary<ulong, uint> observations;
    private readonly HashSet<ulong> changed = new();
    private bool pending;

    internal bool HasUpdates => changed.Count != 0;
    internal bool Changed(ulong id) => changed.Contains(id);

    // Detection is read-only; acknowledging a visit never clears this run's visible changes.
    internal WorkshopHistory(string path, IReadOnlyDictionary<ulong, uint> installed)
    {
        this.path = path;
        observations = Read(path);
        foreach (var item in installed)
        {
            if (item.Key == 0 || item.Value == 0) throw new InvalidDataException("Invalid Workshop update information.");
            if (observations.TryGetValue(item.Key, out uint timestamp))
            {
                if (timestamp == item.Value) continue;
                changed.Add(item.Key);
            }
            observations[item.Key] = item.Value;
            pending = true;
        }
    }

    internal void Acknowledge()
    {
        if (!pending) return;
        Write(path, observations);
        pending = false;
    }

    private static Dictionary<ulong, uint> Read(string path)
    {
        string[] lines;
        try { lines = File.ReadAllLines(path); }
        catch (FileNotFoundException) { return new Dictionary<ulong, uint>(); }
        catch (DirectoryNotFoundException) { return new Dictionary<ulong, uint>(); }
        var result = new Dictionary<ulong, uint>();
        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var fields = line.Split('=');
            if (fields.Length != 2 ||
                !ulong.TryParse(fields[0].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out ulong id) || id == 0 ||
                !uint.TryParse(fields[1].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out uint timestamp) || timestamp == 0 ||
                result.ContainsKey(id))
                throw new InvalidDataException("The saved Workshop update history could not be read: " + path);
            result.Add(id, timestamp);
        }
        return result;
    }

    private static void Write(string path, IReadOnlyDictionary<ulong, uint> history)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, true))
                    foreach (var item in history.OrderBy(item => item.Key))
                        writer.WriteLine(item.Key.ToString(CultureInfo.InvariantCulture) + "=" + item.Value.ToString(CultureInfo.InvariantCulture));
                stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
