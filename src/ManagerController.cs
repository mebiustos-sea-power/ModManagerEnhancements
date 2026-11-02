using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using Path = System.IO.Path;
using System.Linq;
using ModManagerEnhancements.Core;
using Noesis;
using SeaPower;
using SeapowerUI;
using SeapowerUI.ViewModels;
using UnityEngine;

namespace ModManagerEnhancements;

internal sealed class ManagerController
{
    private readonly ModMenuView view;
    private readonly Dictionary<Grid, RowBadge> rows = new();
    private readonly List<INotifyPropertyChanged> observed = new();
    private MenuPage? page;
    private Grid? wrapper;
    private object? originalRightPane;
    private Border? details;
    private Button? all, refresh;
    private TrulyObservableCollection<SearchDirectory>? directories;
    private ScanSession? session;
    private ConflictReport? report;
    private ModText text = new("en");
    private Exception? failure;
    private string? selectedId, selectedName;
    private bool mounted, dirty, renderDirty;
    private float scanAt, rowsAt;

    internal ManagerController(ModMenuView view)
    {
        this.view = view;
        view.Loaded += Loaded;
        view.Unloaded += Unloaded;
        if (view.IsLoaded) Loaded(view, null!);
    }

    private void Loaded(object sender, RoutedEventArgs args) => Plugin.Guard(Mount);
    private void Unloaded(object sender, RoutedEventArgs args) => Plugin.Guard(Unmount);

    private void Mount()
    {
        if (mounted) return;
        try
        {
            page = view.Content as MenuPage ?? throw new InvalidOperationException("The mod manager no longer contains a MenuPage.");
            var model = view.DataContext as ModMenuViewModel ?? throw new InvalidOperationException("The mod manager's view model is unavailable.");
            text = new ModText(LanguageResourceHandler.Instance.Language);
            originalRightPane = page.RightPane;
            if (!(originalRightPane is UIElement original)) throw new InvalidOperationException("The mod manager's right pane is not a UI element.");
            using var stream = typeof(ManagerController).Assembly.GetManifestResourceStream("ModManagerEnhancements.DetailsPanel.xaml")
                ?? throw new FileNotFoundException("The embedded override panel is missing.");
            using var reader = new StreamReader(stream);
            details = GUI.ParseXaml(reader.ReadToEnd()) as Border ?? throw new InvalidOperationException("The override panel is not a Border.");
            all = Named<Button>("ShowAllOverrides"); refresh = Named<Button>("RescanOverrides");
            all.Click += All; refresh.Click += Refresh;
            wrapper = new Grid();
            wrapper.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            wrapper.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            page.RightPane = null;
            wrapper.Children.Add(original);
            Grid.SetRow(details, 1); wrapper.Children.Add(details);
            page.RightPane = wrapper;
            session = new ScanSession(new ConflictScanner().Scan, Plugin.Error);
            mounted = true;
            Plugin.Active = this;
            Observe(model.Directories);
            Invalidate();
        }
        catch { Unmount(); throw; }
    }

    internal void Tick()
    {
        if (!mounted) return;
        var model = view.DataContext as ModMenuViewModel ?? throw new InvalidOperationException("The mod manager's view model changed.");
        if (!ReferenceEquals(directories, model.Directories)) { Observe(model.Directories); Invalidate(); }
        if (dirty && Time.realtimeSinceStartup >= scanAt)
        {
            dirty = false;
            string baseGame = Path.Combine(Application.streamingAssetsPath, "original");
            var snapshot = directories!.Select(d => new ModSource(d.DirectoryInfo.FullName, d.Name, d.DirectoryInfo.FullName,
                d.IsChecked, Plugin.SamePath(d.DirectoryInfo.FullName, baseGame))).ToArray();
            if (selectedId != null && !snapshot.Any(d => ConflictReport.SameId(d.Id, selectedId))) selectedId = selectedName = null;
            session!.Request(snapshot);
        }
        var outcome = session!.Poll();
        if (outcome != null && !dirty)
        {
            report = outcome.Report; failure = outcome.Error;
            Render(); renderDirty = true;
        }
        if (renderDirty || Time.realtimeSinceStartup >= rowsAt)
        {
            UpdateRows(); renderDirty = false; rowsAt = Time.realtimeSinceStartup + 0.5f;
        }
    }

    private void Observe(TrulyObservableCollection<SearchDirectory> source)
    {
        if (directories != null) directories.CollectionChanged -= CollectionChanged;
        foreach (var item in observed) item.PropertyChanged -= ItemChanged;
        observed.Clear(); directories = source;
        directories.CollectionChanged += CollectionChanged;
        foreach (var item in source.Cast<INotifyPropertyChanged>()) { item.PropertyChanged += ItemChanged; observed.Add(item); }
    }

    private void CollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        // Reordering can produce remove/insert bursts; snapshot only after they settle.
        Observe(directories!); Invalidate();
    }
    private void ItemChanged(object? sender, PropertyChangedEventArgs args)
    { if (string.IsNullOrEmpty(args.PropertyName) || args.PropertyName == "IsChecked" || args.PropertyName == "DirectoryInfo") Invalidate(); }
    private void Invalidate()
    {
        dirty = true; scanAt = Time.realtimeSinceStartup + 0.2f;
        report = null; failure = null; renderDirty = true; Render();
    }
    private void Render()
    { if (details != null) details.DataContext = new DetailsModel(text, report, selectedId, selectedName, failure); }
    private void All(object sender, RoutedEventArgs args) => Plugin.Guard(() => { selectedId = selectedName = null; Render(); });
    private void Refresh(object sender, RoutedEventArgs args) => Plugin.Guard(Invalidate);
    private void Open(SearchDirectory source)
    { selectedId = source.DirectoryInfo.FullName; selectedName = source.Name; Render(); }

    private void UpdateRows()
    {
        if (!(page!.ShortLeftPane is DependencyObject left)) throw new InvalidOperationException("The mod manager's list pane is unavailable.");
        var visible = new Dictionary<Grid, SearchDirectory>();
        Visit(left, node =>
        {
            if (node is CheckBox check && check.DataContext is SearchDirectory source && check.Parent is Grid row && Grid.GetColumn(check) == 2)
                visible[row] = source;
        });
        foreach (var pair in rows.ToArray())
            if (!visible.TryGetValue(pair.Key, out var source) || !ReferenceEquals(source, pair.Value.Source))
            { pair.Value.Dispose(); rows.Remove(pair.Key); }
        foreach (var pair in visible)
        {
            if (!rows.TryGetValue(pair.Key, out var badge)) rows.Add(pair.Key, badge = new RowBadge(pair.Key, pair.Value, Open));
            badge.Update(report, text);
        }
    }

    private static void Visit(DependencyObject node, Action<DependencyObject> action)
    {
        action(node);
        int count = VisualTreeHelper.GetChildrenCount(node);
        for (int i = 0; i < count; i++) Visit(VisualTreeHelper.GetChild(node, i), action);
    }
    private T Named<T>(string name) where T : class => details!.FindName(name) as T
        ?? throw new InvalidOperationException("The override panel control is missing: " + name);

    internal void Fail(Exception error)
    {
        Plugin.Error(error);
        failure = error; report = null; Render();
        Unmount();
    }

    private void Unmount()
    {
        if (ReferenceEquals(Plugin.Active, this)) Plugin.Active = null;
        mounted = false;
        session?.Dispose(); session = null;
        if (directories != null) directories.CollectionChanged -= CollectionChanged;
        foreach (var item in observed) item.PropertyChanged -= ItemChanged;
        observed.Clear(); directories = null;
        foreach (var badge in rows.Values) badge.Dispose();
        rows.Clear();
        if (all != null) all.Click -= All;
        if (refresh != null) refresh.Click -= Refresh;
        if (wrapper != null && originalRightPane is UIElement original) wrapper.Children.Remove(original);
        if (page != null && wrapper != null && (page.RightPane == null || ReferenceEquals(page.RightPane, wrapper))) page.RightPane = originalRightPane;
        page = null; wrapper = null; details = null; originalRightPane = null; all = refresh = null;
        report = null; failure = null; selectedId = selectedName = null;
    }
}
