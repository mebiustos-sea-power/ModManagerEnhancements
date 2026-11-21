using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using ModManagerEnhancements.Core;
using SeaPower;

namespace ModManagerEnhancements;

internal static class CompatibilityMetadata
{
    private static readonly ConditionalWeakTable<SearchDirectory, Snapshot> Overrides = new();
    private static readonly FieldInfo Field = typeof(SearchDirectory).GetField("_compatibility", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(typeof(SearchDirectory).FullName, "_compatibility");

    internal static bool IsOutOfRange(SearchDirectory source) => source.HasCompatibilityInfo && !source.MatchesVersion();

    internal static CompatibilityOverrideState GetOverride(SearchDirectory source, Action<Exception> logError)
    {
        var snapshot = Overrides.GetValue(source, _ => new Snapshot());
        try
        {
            var file = new FileInfo(Path.Combine(source.DirectoryInfo.FullName, "_info.ini"));
            if (!file.Exists) { Overrides.Remove(source); return CompatibilityOverrideState.None; }
            if (snapshot.Sampled && snapshot.Length == file.Length && snapshot.Written == file.LastWriteTimeUtc)
                return snapshot.State;
            snapshot.Sampled = true; snapshot.Length = file.Length; snapshot.Written = file.LastWriteTimeUtc;
            snapshot.State = CompatibilityEdit.Inspect(file.FullName);
            snapshot.Error = null;
        }
        catch (Exception error)
        {
            snapshot.State = CompatibilityOverrideState.Unreadable;
            if (snapshot.Error != error.Message) logError(error);
            snapshot.Error = error.Message;
        }
        return snapshot.State;
    }

    internal static void Reload(SearchDirectory source, IEnumerable<SearchDirectory> candidates)
    {
        string folder = source.DirectoryInfo.FullName;
        string path = Path.Combine(folder, "_info.ini");
        var ini = IniRefresh.Read(path,
            filename => Utils.openIniFile(filename, true, true, false)
                ?? throw new IOException("Could not reload the mod's _info.ini: " + filename),
            handler => handler.getFileName(), IniHandler.invalidateCache, handler => handler.reloadFromDisk());
        var compatibility = ModCompatibility.FromIni(ini);
        // SearchDirectory caches this metadata and has no public reload method.
        foreach (var item in candidates.Append(source).Distinct().Where(item =>
            string.Equals(item.DirectoryInfo.FullName, folder, StringComparison.OrdinalIgnoreCase)))
        {
            Field.SetValue(item, compatibility);
            Overrides.Remove(item);
        }
    }

    private sealed class Snapshot
    {
        internal bool Sampled;
        internal long Length;
        internal DateTime Written;
        internal CompatibilityOverrideState State;
        internal string? Error;
    }
}
