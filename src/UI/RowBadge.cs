using System;
using System.Linq;
using ModManagerEnhancements.Core;
using Noesis;
using SeaPower;

namespace ModManagerEnhancements;

internal sealed class RowBadge : IDisposable
{
    internal SearchDirectory Source { get; }
    private readonly Grid row;
    private readonly RowMetrics metrics;
    private readonly Button button;
    private readonly Border dllBadge;
    private readonly TextBlock preferred = new() { Name = "ModManagerEnhancementsPreferred" };
    private readonly TextBlock overridden = new() { Name = "ModManagerEnhancementsOverridden" };
    private readonly TextBlock dllLabel;
    private readonly Border selection;
    private readonly Brush normal = new SolidColorBrush(Color.FromArgb(255, 217, 183, 111));
    private readonly Brush highlight = new SolidColorBrush(Color.FromArgb(255, 126, 217, 242));
    private readonly Brush selectedBackground = new SolidColorBrush(Color.FromArgb(255, 35, 65, 75));
    private readonly Brush transparent = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
    private readonly ColumnDefinition column, dllColumn;
    private readonly Action<SearchDirectory> open;
    private readonly (UIElement Child, int Column)[] shifted;
    private readonly (ColumnDefinition Column, GridLength Width)[] nativeGaps;
    private SourceIndicator? sourceIndicator;

    internal RowBadge(Grid row, SearchDirectory source, Action<SearchDirectory> open, RowMetrics metrics)
    {
        this.row = row; Source = source; this.open = open; this.metrics = metrics;
        if (row.ColumnDefinitions.Count != 9) throw new InvalidOperationException("The mod row no longer has the expected grid layout.");
        column = new ColumnDefinition(); dllColumn = new ColumnDefinition { Width = GridLength.Auto };
        nativeGaps = new[] { 5, 7 }.Select(i => (row.ColumnDefinitions[i], row.ColumnDefinitions[i].Width)).ToArray();
        shifted = row.Children.Cast<UIElement>().Where(c => Grid.GetColumn(c) >= 5).Select(c => (c, Grid.GetColumn(c))).ToArray();
        var counts = new Grid();
        counts.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        counts.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
        counts.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        counts.Children.Add(preferred); Grid.SetColumn(overridden, 2); counts.Children.Add(overridden);
        selection = new Border { Child = counts, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(2),
            Padding = new Thickness(2, 0, 2, 0), BorderBrush = transparent, Background = transparent };
        dllLabel = new TextBlock { Text = "DLL", Foreground = new SolidColorBrush(Color.FromArgb(255, 148, 158, 166)) };
        dllBadge = new Border { Name = "ModManagerEnhancementsDllBadge", Padding = new Thickness(2, 0, 2, 0),
            Margin = new Thickness(4, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed,
            BorderThickness = new Thickness(0), Background = transparent, Child = dllLabel };
        button = new Button { Name = "ModManagerEnhancementsOverrides", MinWidth = 0,
            HorizontalAlignment = HorizontalAlignment.Right, HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(4, 1, 4, 1), Margin = new Thickness(4, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed, Content = selection };
        button.Style = row.Children.OfType<Button>().Select(b => b.Style).FirstOrDefault(s => s != null);
        button.Click += Click;
        try
        {
            sourceIndicator = new SourceIndicator(row);
            // Icon and DLL margins supply spacing around the native dependency warning.
            foreach (var gap in nativeGaps) gap.Column.Width = new GridLength(0);
            // Keep native A before DLL, with source indicators and fixed override slots on the right.
            row.ColumnDefinitions.Insert(5, dllColumn);
            row.ColumnDefinitions.Add(column);
            foreach (var item in shifted) Grid.SetColumn(item.Child, item.Column + 1);
            Grid.SetColumn(dllBadge, 5); row.Children.Add(dllBadge);
            Grid.SetColumn(button, 10); row.Children.Add(button);
        }
        catch { Dispose(); throw; }
    }

    internal void Update(BadgeModel model, bool containsDll, ModText text)
    {
        metrics.Update(preferred);
        preferred.Width = overridden.Width = metrics.DirectionWidth;
        column.Width = new GridLength(metrics.OverrideColumnWidth); button.Width = metrics.ButtonWidth;
        sourceIndicator!.Update(metrics, text, Source.IsSteam);
        dllLabel.FontSize = preferred.FontSize * 0.85f;
        dllBadge.ToolTip = text.DllTooltip;
        dllBadge.Visibility = containsDll ? Visibility.Visible : Visibility.Collapsed;
        preferred.Text = model.PreferredText; overridden.Text = model.OverriddenText;
        preferred.Foreground = model.HighlightPreferred ? highlight : normal;
        overridden.Foreground = model.HighlightOverridden ? highlight : normal;
        selection.BorderBrush = model.IsSelected ? highlight : transparent;
        selection.Background = model.IsSelected ? selectedBackground : transparent;
        button.ToolTip = text.Tooltip(model.Impact, model.IsSelected);
        button.Visibility = model.Impact.HasOverlaps ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Click(object sender, RoutedEventArgs args)
    { args.Handled = true; Plugin.Guard(() => open(Source)); }

    public void Dispose()
    {
        button.Click -= Click;
        row.Children.Remove(button); row.Children.Remove(dllBadge);
        sourceIndicator?.Dispose(); sourceIndicator = null;
        foreach (var gap in nativeGaps) gap.Column.Width = gap.Width;
        if (row.ColumnDefinitions.Contains(column)) row.ColumnDefinitions.Remove(column);
        if (row.ColumnDefinitions.Contains(dllColumn))
        {
            row.ColumnDefinitions.Remove(dllColumn);
            foreach (var item in shifted) if (row.Children.Contains(item.Child)) Grid.SetColumn(item.Child, item.Column);
        }
    }
}
