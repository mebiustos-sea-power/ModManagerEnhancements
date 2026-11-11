using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;

namespace ModManagerEnhancements.Core;

// Input order follows the game's search order. The first remaining provider has priority.
// INI key merges and DLL loading are outside the scope of this file index.
internal sealed class ConflictScanner
{
    internal ConflictReport Scan(IReadOnlyList<ModSource> sources, CancellationToken cancellation)
    {
        var index = new Dictionary<string, List<(ModSource Mod, string Path)>>(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var replacedFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            cancellation.ThrowIfCancellationRequested();
            if (!source.Enabled || source.IsBaseGame) continue;
            if (!seen.Add(source.Id)) throw new InvalidOperationException("A mod directory was registered twice: " + source.Directory);
            var root = new DirectoryInfo(source.Directory);
            if (!root.Exists) throw new DirectoryNotFoundException("The mod directory is missing: " + source.Directory);
            foreach (var path in ModFiles.Enumerate(root, cancellation))
            {
                string relative = Path.GetRelativePath(root.FullName, path).Replace('\\', '/');
                if (IsReplaced(relative, replacedFolders) || !IsContent(relative)) continue;
                if (!index.TryGetValue(relative, out var providers)) index.Add(relative, providers = new List<(ModSource, string)>());
                providers.Add((source, path));
            }

            // The game checks this layer's files before stopping the search of lower layers.
            foreach (string folder in source.ReplacedFolders)
            {
                string normalized = folder.Replace('\\', '/').Trim('/');
                while (normalized.Contains("//")) normalized = normalized.Replace("//", "/");
                if (normalized.Length > 0) replacedFolders.Add(normalized);
            }
        }

        var overlaps = new List<FileOverlap>();
        foreach (var pair in index.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            cancellation.ThrowIfCancellationRequested();
            if (pair.Value.Count < 2) continue;
            var providers = new List<FileProvider>();
            foreach (var candidate in pair.Value)
                providers.Add(new FileProvider(candidate.Mod, candidate.Path, Fingerprint(candidate.Path, cancellation)));
            if (providers.All(p => p.Fingerprint == providers[0].Fingerprint)) continue;
            overlaps.Add(new FileOverlap(pair.Key, providers.AsReadOnly()));
        }
        return new ConflictReport(overlaps.AsReadOnly());
    }

    private static bool IsReplaced(string relative, HashSet<string> replacedFolders)
    {
        foreach (string folder in replacedFolders)
        {
            if (relative.Equals(folder, StringComparison.OrdinalIgnoreCase) ||
                (relative.Length > folder.Length && relative[folder.Length] == '/' &&
                    relative.StartsWith(folder, StringComparison.OrdinalIgnoreCase))) return true;
        }
        return false;
    }

    private static bool IsContent(string relative)
    {
        string extension = Path.GetExtension(relative);
        if (new[] { ".dll", ".exe", ".pdb", ".mdb" }.Contains(extension, StringComparer.OrdinalIgnoreCase)) return false;
        int separator = relative.LastIndexOf('/');
        if (separator >= 0)
        {
            // The game keeps direct background PNGs as independent candidates from every mod.
            return !(extension.Equals(".png", StringComparison.OrdinalIgnoreCase) &&
                relative.Substring(0, separator).Equals("ui/backgrounds", StringComparison.OrdinalIgnoreCase));
        }
        string name = relative.ToLowerInvariant();
        return name != "_info.ini" && name != "thumbnail.png" &&
            !name.StartsWith("readme", StringComparison.Ordinal) && !name.StartsWith("license", StringComparison.Ordinal);
    }

    private static string Fingerprint(string path, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var hash = SHA256.Create();
        byte[] digest = hash.ComputeHash(stream);
        cancellation.ThrowIfCancellationRequested();
        return Convert.ToBase64String(digest);
    }
}
