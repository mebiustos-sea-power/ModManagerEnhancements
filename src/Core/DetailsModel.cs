using System;
using System.Collections.Generic;
using System.Linq;

namespace ModManagerEnhancements.Core;

// These public properties are consumed by Noesis bindings; the model owns no UI objects.
internal sealed class FileDetail
{
    public string Path { get; }
    public string Providers { get; }
    public string Locations { get; }
    public string Note { get; }
    internal FileDetail(FileOverlap file, ModText text)
    {
        Path = file.RelativePath;
        Providers = string.Join("\n", file.Providers.Select((p, i) =>
            (i == 0 ? text.Preferred : p.Fingerprint == file.Preferred.Fingerprint ? text.Identical : text.Lower) + ": " + p.Mod.Name));
        Locations = string.Join("\n", file.Providers.Select(p => p.Mod.Name + "\n" + p.FullPath));
        Note = file.IsIni ? text.IniNote : "";
    }
}

internal sealed class DetailsModel
{
    public string Header { get; }
    public string Help { get; }
    public string Status { get; }
    public bool IsFiltered { get; }
    public string Selection { get; }
    public string AllCaption { get; }
    public string RefreshCaption { get; }
    public string Error { get; }
    public string Empty { get; }
    public IReadOnlyList<FileDetail> Files { get; }

    internal DetailsModel(ModText text, ConflictReport? report, string? selectedId, string? selectedName, Exception? error)
    {
        Header = text.Header; Help = text.Help;
        AllCaption = text.ShowAll; RefreshCaption = text.Refresh;
        IsFiltered = selectedId != null;
        Selection = IsFiltered ? text.FilteredSelection(selectedName
            ?? throw new ArgumentException("A selected mod requires a display name.", nameof(selectedName))) : "";
        Error = error?.Message ?? "";
        var files = report?.Files.Where(f => selectedId == null || f.Providers.Any(p => ConflictReport.SameId(p.Mod.Id, selectedId))).ToArray();
        Files = files?.Select(f => new FileDetail(f, text)).ToArray() ?? Array.Empty<FileDetail>();
        Status = error != null ? text.Failed : report == null ? text.Scanning : text.Summary(Files.Count);
        Empty = report != null && Files.Count == 0 ? text.Empty : "";
    }
}
