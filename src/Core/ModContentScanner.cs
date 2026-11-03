using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace ModManagerEnhancements.Core;

[Flags]
internal enum ModContent { None = 0, Missions = 1, Campaigns = 2, Units = 4, Dll = 8 }

internal sealed class ModContentInventory
{
    private readonly Dictionary<string, ModContent> contents;
    internal ModContentInventory(IDictionary<string, ModContent> contents)
    { this.contents = new Dictionary<string, ModContent>(contents, StringComparer.OrdinalIgnoreCase); }
    internal ModContent Kinds(string modId) => contents.TryGetValue(modId, out var value) ? value : ModContent.None;
    internal bool ContainsDll(string modId) => (Kinds(modId) & ModContent.Dll) != 0;
}

// Content presence is independent of enable state. File contents and assemblies are never opened.
internal sealed class ModContentScanner
{
    internal ModContentInventory Scan(IReadOnlyList<ModSource> sources, CancellationToken cancellation)
    {
        var found = new Dictionary<string, ModContent>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            cancellation.ThrowIfCancellationRequested();
            if (source.IsBaseGame) continue;
            if (found.ContainsKey(source.Id)) throw new InvalidOperationException("A mod directory was registered twice: " + source.Directory);
            var root = new DirectoryInfo(source.Directory);
            if (!root.Exists) throw new DirectoryNotFoundException("The mod directory is missing: " + source.Directory);
            var kinds = ModContent.None;
            foreach (string path in ModFiles.Enumerate(root, cancellation, includeHiddenDirectories: true))
                kinds |= Classify(Path.GetRelativePath(root.FullName, path).Replace('\\', '/'));
            found.Add(source.Id, kinds);
        }
        return new ModContentInventory(found);
    }

    private static ModContent Classify(string relative)
    {
        string extension = Path.GetExtension(relative);
        if (extension.Equals(".dll", StringComparison.OrdinalIgnoreCase)) return ModContent.Dll;
        if (!extension.Equals(".ini", StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileName(relative).Equals("_info.ini", StringComparison.OrdinalIgnoreCase)) return ModContent.None;
        string[] parts = relative.Split('/');
        if (parts.Length < 2) return ModContent.None;
        switch (parts[0].ToLowerInvariant())
        {
            case "missions":
                for (int i = 1; i < parts.Length - 1; i++)
                    if (parts[i].StartsWith(".", StringComparison.Ordinal) ||
                        parts[i].EndsWith("_data", StringComparison.OrdinalIgnoreCase) ||
                        parts[i].EndsWith("_briefing", StringComparison.OrdinalIgnoreCase) ||
                        parts[i].EndsWith("_temp", StringComparison.OrdinalIgnoreCase)) return ModContent.None;
                return ModContent.Missions;
            case "campaigns": return ModContent.Campaigns;
            case "aircraft": case "vessels": case "land_units": case "biologic": case "ammunition":
                return ModContent.Units;
            default: return ModContent.None;
        }
    }
}
