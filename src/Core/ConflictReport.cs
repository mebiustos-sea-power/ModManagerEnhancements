using System;
using System.Collections.Generic;
using System.Linq;

namespace ModManagerEnhancements.Core;

internal sealed class ModSource
{
    internal string Id { get; }
    internal string Name { get; }
    internal string Directory { get; }
    internal bool Enabled { get; }
    internal bool IsBaseGame { get; }

    internal ModSource(string id, string name, string directory, bool enabled = true, bool isBaseGame = false)
    { Id = id; Name = name; Directory = directory; Enabled = enabled; IsBaseGame = isBaseGame; }
}

internal sealed class FileProvider
{
    internal ModSource Mod { get; }
    internal string FullPath { get; }
    internal string Fingerprint { get; }
    internal FileProvider(ModSource mod, string fullPath, string fingerprint)
    { Mod = mod; FullPath = fullPath; Fingerprint = fingerprint; }
}

internal sealed class FileOverlap
{
    internal string RelativePath { get; }
    internal IReadOnlyList<FileProvider> Providers { get; }
    internal FileProvider Preferred => Providers[0];
    internal bool IsIni => RelativePath.EndsWith(".ini", StringComparison.OrdinalIgnoreCase);
    internal FileOverlap(string relativePath, IReadOnlyList<FileProvider> providers)
    { RelativePath = relativePath; Providers = providers; }
}

internal readonly struct ModImpact
{
    internal int Preferred { get; }
    internal int Overridden { get; }
    internal bool HasOverlaps => Preferred + Overridden > 0;
    internal ModImpact(int preferred, int overridden) { Preferred = preferred; Overridden = overridden; }
}

internal sealed class ConflictReport
{
    internal IReadOnlyList<FileOverlap> Files { get; }
    internal ConflictReport(IReadOnlyList<FileOverlap> files) { Files = files; }

    internal ModImpact Impact(string modId)
    {
        int preferred = 0, overridden = 0;
        foreach (var file in Files)
        {
            if (SameId(file.Preferred.Mod.Id, modId)) preferred++;
            else if (file.Providers.Any(p => SameId(p.Mod.Id, modId) && p.Fingerprint != file.Preferred.Fingerprint)) overridden++;
        }
        return new ModImpact(preferred, overridden);
    }

    internal static bool SameId(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
