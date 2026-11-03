using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using ModManagerEnhancements.Core;
using Noesis;
using SeaPower;

namespace ModManagerEnhancements;

internal sealed class ModListSearch : IDisposable
{
    private static readonly ModContent[] Choices = { ModContent.None, ModContent.Missions, ModContent.Campaigns, ModContent.Units, ModContent.Dll };
    private readonly Grid host;
    private readonly ItemsControl list;
    private readonly RowDefinition addedRow = new() { Height = GridLength.Auto };
    private readonly (UIElement Child, int Row)[] shifted;
    private readonly NativeProperty itemsSource, scrollMargin;
    private readonly ObservableCollection<SearchDirectory> visible = new();
    private readonly Dictionary<Grid, RowScope> scopes = new();
    private readonly ModText text;
    private readonly Action changed;
    private readonly ICommand nativeAll, nativeNone, scopedAll, scopedNone;
    private IList<SearchDirectory> source;
    private ModContentInventory? inventory;
    private Exception? inventoryError;
    private ModListFilter filter = new("", ModContent.None);
    private bool disposed;

    internal Grid Header { get; }
    internal TextBox SearchBox { get; }
    internal ComboBox KindBox { get; }
    internal Button ClearButton { get; }
    internal TextBlock Counter { get; }
    private readonly TextBlock placeholder, empty;
    internal IReadOnlyList<SearchDirectory> Visible => visible;
    internal bool IsFiltered => filter.IsActive;

    internal ModListSearch(Grid host, ItemsControl list, IList<SearchDirectory> source,
        ICommand nativeAll, ICommand nativeNone, ModText text, Action changed)
    {
        this.host = host; this.list = list; this.source = source; this.text = text; this.changed = changed;
        this.nativeAll = nativeAll; this.nativeNone = nativeNone;
        scopedAll = new SelectionCommand(this, nativeAll, true); scopedNone = new SelectionCommand(this, nativeNone, false);
        itemsSource = new NativeProperty(list, ItemsControl.ItemsSourceProperty);
        UIElement direct = list;
        while (!ReferenceEquals(VisualTreeHelper.GetParent(direct), host))
            direct = VisualTreeHelper.GetParent(direct) as UIElement ?? throw new InvalidOperationException("The native MOD list is not inside its list pane.");
        if (!(direct is ScrollViewer scroll)) throw new InvalidOperationException("The native MOD list no longer has its expected scroll viewer.");
        scrollMargin = new NativeProperty(scroll, FrameworkElement.MarginProperty);
        int row = Grid.GetRow(direct);
        shifted = host.Children.Cast<UIElement>().Where(c => Grid.GetRow(c) >= row).Select(c => (c, Grid.GetRow(c))).ToArray();
        using var stream = typeof(ModListSearch).Assembly.GetManifestResourceStream("ModManagerEnhancements.ModListSearch.xaml")
            ?? throw new FileNotFoundException("The embedded MOD search controls are missing.");
        using var reader = new StreamReader(stream);
        Header = GUI.ParseXaml(reader.ReadToEnd()) as Grid ?? throw new InvalidOperationException("The search controls are not a Grid.");
        SearchBox = Named<TextBox>("SearchText"); KindBox = Named<ComboBox>("ContentKind");
        ClearButton = Named<Button>("ClearSearch"); Counter = Named<TextBlock>("VisibleCount");
        placeholder = Named<TextBlock>("SearchPlaceholder"); empty = Named<TextBlock>("NoMatchingMods");
        placeholder.Text = text.SearchPlaceholder; ClearButton.ToolTip = text.ClearSearch;
        foreach (var choice in Choices) KindBox.Items.Add(new ComboBoxItem { Content = text.KindName(choice) });
        KindBox.SelectedIndex = 0;
        try
        {
            host.RowDefinitions.Insert(row, addedRow);
            foreach (var item in shifted) Grid.SetRow(item.Child, item.Row + 1);
            Grid.SetRow(Header, row); host.Children.Add(Header);
            var margin = scroll.Margin; scroll.Margin = new Thickness(margin.Left, 10, margin.Right, margin.Bottom);
            list.ItemsSource = visible;
            SearchBox.TextChanged += Changed; KindBox.SelectionChanged += Changed; ClearButton.Click += Clear;
            Refresh();
        }
        catch { Dispose(); throw; }
    }

    internal void SetSource(IList<SearchDirectory> value) { source = value; Refresh(); }
    internal void SetInventory(ModContentInventory? value, Exception? error)
    { inventory = value; inventoryError = error; Refresh(); }
    private void Changed(object sender, RoutedEventArgs args) => Plugin.Guard(Refresh);
    private void Clear(object sender, RoutedEventArgs args) => Plugin.Guard(() => { SearchBox.Text = ""; SearchBox.Focus(); });

    internal void Refresh()
    {
        if (disposed) return;
        filter = new ModListFilter(SearchBox.Text, Choices[Math.Max(0, KindBox.SelectedIndex)]);
        bool waiting = filter.Kind != ModContent.None && inventory == null;
        var wanted = waiting ? Array.Empty<SearchDirectory>() : source.Where(item => filter.Matches(item.Name,
            item.IsSteam ? item.steamId.ToString() : "", inventory?.Kinds(item.DirectoryInfo.FullName) ?? ModContent.None)).ToArray();
        // The projection keeps the original objects and load order; it never edits the saved list.
        for (int i = 0; i < wanted.Length; i++)
        {
            if (i < visible.Count && ReferenceEquals(visible[i], wanted[i])) continue;
            int old = visible.IndexOf(wanted[i]);
            if (old >= 0) visible.Move(old, i); else visible.Insert(i, wanted[i]);
        }
        while (visible.Count > wanted.Length) visible.RemoveAt(visible.Count - 1);
        Counter.Text = waiting ? $"— / {source.Count}" : $"{visible.Count} / {source.Count}";
        placeholder.Visibility = SearchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        ClearButton.Visibility = SearchBox.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        empty.Text = waiting ? (inventoryError == null ? text.KindScanning : text.KindFailed) : text.NoMatchingMods;
        empty.Visibility = visible.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        changed();
    }

    internal void UpdateRows(IEnumerable<Grid> rows)
    {
        var current = new HashSet<Grid>(rows);
        foreach (var pair in scopes.ToArray())
            if (!current.Contains(pair.Key)) { pair.Value.Dispose(); scopes.Remove(pair.Key); }
        foreach (var row in current)
            if (!scopes.ContainsKey(row))
                scopes.Add(row, new RowScope(row, nativeAll, nativeNone, scopedAll, scopedNone));
    }

    private T Named<T>(string name) where T : class => Header.FindName(name) as T
        ?? throw new InvalidOperationException("The search control is missing: " + name);

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        SearchBox.TextChanged -= Changed; KindBox.SelectionChanged -= Changed; ClearButton.Click -= Clear;
        foreach (var scope in scopes.Values) scope.Dispose(); scopes.Clear();
        itemsSource.Restore(); scrollMargin.Restore();
        host.Children.Remove(Header);
        if (host.RowDefinitions.Contains(addedRow))
        {
            host.RowDefinitions.Remove(addedRow);
            foreach (var item in shifted) Grid.SetRow(item.Child, item.Row);
        }
        visible.Clear();
    }

    private sealed class SelectionCommand : ICommand
    {
        private readonly ModListSearch owner;
        private readonly ICommand original;
        private readonly bool enabled;
        internal SelectionCommand(ModListSearch owner, ICommand original, bool enabled)
        { this.owner = owner; this.original = original; this.enabled = enabled; }
        public bool CanExecute(object? parameter) => original.CanExecute(parameter);
        public void Execute(object? parameter) => Plugin.Guard(() =>
        {
            if (owner.disposed || !owner.IsFiltered) { original.Execute(parameter); return; }
            foreach (var item in owner.visible.ToArray()) if (item.IsEnabled) item.IsChecked = enabled;
        });
        public event System.EventHandler? CanExecuteChanged
        { add { original.CanExecuteChanged += value; } remove { original.CanExecuteChanged -= value; } }
    }

    private sealed class RowScope : IDisposable
    {
        private readonly NativeProperty[] commands;
        internal RowScope(Grid row, ICommand all, ICommand none, ICommand scopedAll, ICommand scopedNone)
        {
            var menu = row.ContextMenu ?? throw new InvalidOperationException("The native MOD row context menu is missing.");
            MenuItem Find(string name, ICommand command) => menu.Items.OfType<MenuItem>().SingleOrDefault(item =>
                ReferenceEquals(item.Command, command) || (BindingOperations.GetBindingBase(item, MenuItem.CommandProperty) is Binding binding &&
                    binding.Path?.Path == "DataContext." + name))
                ?? throw new InvalidOperationException("The native MOD action is missing: " + name);
            var actions = new[] { Find("SelectAllCommand", all), Find("SelectNoneCommand", none) };
            commands = actions.Select(item => new NativeProperty(item, MenuItem.CommandProperty)).ToArray();
            try { actions[0].Command = scopedAll; actions[1].Command = scopedNone; }
            catch { Dispose(); throw; }
        }
        public void Dispose()
        { foreach (var command in commands) command.Restore(); }
    }
}
