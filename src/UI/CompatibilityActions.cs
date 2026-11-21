using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Path = System.IO.Path;
using ModManagerEnhancements.Core;
using Noesis;
using SeaPower;

namespace ModManagerEnhancements;

internal sealed class CompatibilityActions : IDisposable
{
    private readonly string version;
    private readonly ModText text;
    private readonly Action<NativeMessageBox> show, close;
    private readonly Func<NativeMessageBox, bool> isOpen;
    private readonly Action<SearchDirectory, bool> reload;
    private readonly Action<Exception> logError;
    private readonly Dictionary<Grid, Entry> entries = new();
    private NativeMessageBox? dialog;
    private bool disposed;

    internal CompatibilityActions(string version, ModText text, Action<NativeMessageBox> show,
        Action<NativeMessageBox> close, Func<NativeMessageBox, bool> isOpen,
        Action<SearchDirectory, bool> reload, Action<Exception> logError)
    {
        this.version = version; this.text = text; this.show = show; this.close = close;
        this.isOpen = isOpen; this.reload = reload; this.logError = logError;
    }

    internal void UpdateRows(IReadOnlyDictionary<Grid, CompatibilityRow> rows)
    {
        foreach (var pair in entries.ToArray())
            if (!rows.TryGetValue(pair.Key, out var row) || !ReferenceEquals(row.Source, pair.Value.Source) ||
                row.OutOfRange != pair.Value.Override || row.CanRestore != pair.Value.Restore ||
                !ReferenceEquals(pair.Key.ContextMenu, pair.Value.Menu))
            { pair.Value.Dispose(); entries.Remove(pair.Key); }
        foreach (var pair in rows)
        {
            var row = pair.Value;
            if (entries.ContainsKey(pair.Key) || (!row.OutOfRange && !row.CanRestore)) continue;
            var source = row.Source;
            var menu = pair.Key.ContextMenu ?? throw new InvalidOperationException("The native MOD row context menu is missing.");
            var items = new List<MenuItem>();
            if (row.CanRestore)
                items.Add(new MenuItem { Header = text.RestoreCompatibility, Command = new DelegateCommand(_ => Request(source, true)) });
            if (row.OutOfRange)
                items.Add(new MenuItem { Header = text.SetCompatibility, Command = new DelegateCommand(_ => Request(source, false)) });
            for (int i = items.Count - 1; i >= 0; i--) menu.Items.Insert(0, items[i]);
            entries.Add(pair.Key, new Entry(source, menu, items, row.OutOfRange, row.CanRestore));
        }
    }

    private void Request(SearchDirectory source, bool restoring)
    {
        if (disposed || (dialog != null && isOpen(dialog))) return;
        dialog = null;
        try
        {
            string path = Path.Combine(source.DirectoryInfo.FullName, "_info.ini");
            var edit = restoring ? CompatibilityEdit.PrepareRestore(path) : CompatibilityEdit.Prepare(path, version);
            var message = new NativeMessageBox
            {
                Header = restoring ? text.RestoreCompatibility : text.CompatibilityTitle,
                Message = restoring ? text.CompatibilityRestoreConfirmation(source.Name) : text.CompatibilityConfirmation(source.Name, version),
                Kind = "Warning", CancelCommandLabel = text.Cancel,
                AcknowledgeCommandLabel = restoring ? text.RestoreButton : text.Change
            };
            message.CancelCommand = new DelegateCommand(_ => Dismiss(message));
            message.AcknowledgeCommand = new DelegateCommand(_ => Apply(message, source, edit));
            dialog = message;
            show(message);
        }
        catch (Exception error) { Failed(error); }
    }

    private void Apply(NativeMessageBox message, SearchDirectory source, CompatibilityEdit edit)
    {
        if (disposed || !ReferenceEquals(dialog, message) || !isOpen(message)) return;
        try
        {
            edit.Commit();
            reload(source, edit.IsRestore);
            Dismiss(message);
        }
        catch (Exception error) { Dismiss(message); Failed(error); }
    }

    private void Dismiss(NativeMessageBox message)
    {
        if (ReferenceEquals(dialog, message)) dialog = null;
        if (isOpen(message)) close(message);
    }

    private void Failed(Exception error)
    {
        logError(error);
        if (dialog != null) Dismiss(dialog);
        var message = new NativeMessageBox
        {
            Header = text.CompatibilityTitle, Message = text.CompatibilityFailure(error.Message),
            Kind = "Error", AcknowledgeCommandLabel = text.Close
        };
        message.AcknowledgeCommand = new DelegateCommand(_ => Dismiss(message));
        dialog = message;
        try { show(message); }
        catch (Exception displayError) { dialog = null; logError(displayError); }
    }

    public void Dispose()
    {
        disposed = true;
        if (dialog != null) Dismiss(dialog);
        foreach (var entry in entries.Values) entry.Dispose();
        entries.Clear();
    }

    private sealed class Entry : IDisposable
    {
        internal SearchDirectory Source { get; }
        internal ContextMenu Menu { get; }
        internal bool Override { get; }
        internal bool Restore { get; }
        private readonly IReadOnlyList<MenuItem> items;
        internal Entry(SearchDirectory source, ContextMenu menu, IReadOnlyList<MenuItem> items, bool change, bool restore)
        { Source = source; Menu = menu; this.items = items; Override = change; Restore = restore; }
        public void Dispose() { foreach (var item in items) Menu.Items.Remove(item); }
    }
}
