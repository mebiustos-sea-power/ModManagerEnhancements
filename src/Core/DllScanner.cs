using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace ModManagerEnhancements.Core;

internal sealed class DllInventory
{
    private readonly HashSet<string> directories;
    internal DllInventory(IEnumerable<string> directoriesWithDlls)
    { directories = new HashSet<string>(directoriesWithDlls, StringComparer.OrdinalIgnoreCase); }
    internal bool ContainsDll(string modId) => directories.Contains(modId);
}

// Presence is independent of enable state and Workshop metadata. No assembly is opened or loaded.
internal sealed class DllScanner
{
    internal DllInventory Scan(IReadOnlyList<ModSource> sources, CancellationToken cancellation)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var found = new List<string>();
        foreach (var source in sources)
        {
            cancellation.ThrowIfCancellationRequested();
            if (source.IsBaseGame) continue;
            if (!seen.Add(source.Id)) throw new InvalidOperationException("A mod directory was registered twice: " + source.Directory);
            var root = new DirectoryInfo(source.Directory);
            if (!root.Exists) throw new DirectoryNotFoundException("The mod directory is missing: " + source.Directory);
            foreach (string path in ModFiles.Enumerate(root, cancellation, includeHiddenDirectories: true))
                if (string.Equals(Path.GetExtension(path), ".dll", StringComparison.OrdinalIgnoreCase))
                { found.Add(source.Id); break; }
        }
        return new DllInventory(found);
    }
}
