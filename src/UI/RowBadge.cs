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
    private readonly Button button;
    private readonly Border dllBadge;
    private readonly TextBlock label = new(), dllLabel;
    private readonly Border selection;
    private readonly Run preferred = new(), separator = new(), overridden = new();
    private readonly Brush normal = new SolidColorBrush(Color.FromArgb(255, 217, 183, 111));
    private readonly Brush highlight = new SolidColorBrush(Color.FromArgb(255, 126, 217, 242));
    private readonly Brush selectedBackground = new SolidColorBrush(Color.FromArgb(255, 35, 65, 75));
    private readonly Brush transparent = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
    private readonly ColumnDefinition column, dllColumn;
    private readonly Action<SearchDirectory> open;
    private readonly (UIElement Child, int Column)[] shifted;

    internal RowBadge(Grid row, SearchDirectory source, Action<SearchDirectory> open)
    {
        this.row = row; Source = source; this.open = open;
        if (row.ColumnDefinitions.Count < 9) throw new InvalidOperationException("The mod row no longer has the expected grid layout.");
        column = new ColumnDefinition { Width = GridLength.Auto };
        dllColumn = new ColumnDefinition { Width = GridLength.Auto };
        shifted = row.Children.Cast<UIElement>().Where(c => Grid.GetColumn(c) >= 4).Select(c => (c, Grid.GetColumn(c))).ToArray();
        label.Inlines.Add(preferred); label.Inlines.Add(separator); label.Inlines.Add(overridden);
        selection = new Border { Child = label, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(2),
            Padding = new Thickness(2, 0, 2, 0), BorderBrush = transparent, Background = transparent };
        dllLabel = new TextBlock { Text = "DLL", Foreground = new SolidColorBrush(Color.FromArgb(255, 148, 158, 166)) };
        dllBadge = new Border { Name = "ModManagerEnhancementsDllBadge", Padding = new Thickness(2, 0, 2, 0),
            Margin = new Thickness(4, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed,
            BorderThickness = new Thickness(0), Background = transparent, Child = dllLabel };
        button = new Button { Name = "ModManagerEnhancementsOverrides", MinWidth = 0, HorizontalAlignment = HorizontalAlignment.Right, Padding = new Thickness(4, 1, 4, 1), Margin = new Thickness(4, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed, Content = selection };
        button.Style = row.Children.OfType<Button>().Select(b => b.Style).FirstOrDefault(s => s != null);
        button.Click += Click;
        // Keep DLL before A and overrides before the original Workshop/Local source column.
        try
        {
            row.ColumnDefinitions.Insert(4, dllColumn);
            row.ColumnDefinitions.Insert(9, column);
            foreach (var item in shifted) Grid.SetColumn(item.Child, item.Column + (item.Column >= 8 ? 2 : 1));
            Grid.SetColumn(dllBadge, 4); row.Children.Add(dllBadge);
            Grid.SetColumn(button, 9);
            row.Children.Add(button);
        }
        catch { Dispose(); throw; }
    }

    internal void Update(BadgeModel model, bool containsDll, ModText text)
    {
        dllLabel.FontSize = label.FontSize * 0.85f;
        dllBadge.ToolTip = text.DllTooltip;
        dllBadge.Visibility = containsDll ? Visibility.Visible : Visibility.Collapsed;
        var impact = model.Impact;
        preferred.Text = impact.Preferred > 0 ? "↑" + impact.Preferred : "";
        separator.Text = impact.Preferred > 0 && impact.Overridden > 0 ? " " : "";
        overridden.Text = impact.Overridden > 0 ? "↓" + impact.Overridden : "";
        preferred.Foreground = model.HighlightPreferred ? highlight : normal;
        overridden.Foreground = model.HighlightOverridden ? highlight : normal;
        selection.BorderBrush = model.IsSelected ? highlight : transparent;
        selection.Background = model.IsSelected ? selectedBackground : transparent;
        button.ToolTip = text.Tooltip(impact, model.IsSelected);
        button.Visibility = impact.HasOverlaps ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Click(object sender, RoutedEventArgs args)
    { args.Handled = true; Plugin.Guard(() => open(Source)); }

    public void Dispose()
    {
        button.Click -= Click;
        row.Children.Remove(button); row.Children.Remove(dllBadge);
        if (row.ColumnDefinitions.Contains(column)) row.ColumnDefinitions.Remove(column);
        if (row.ColumnDefinitions.Contains(dllColumn))
        {
            row.ColumnDefinitions.Remove(dllColumn);
            foreach (var item in shifted) if (row.Children.Contains(item.Child)) Grid.SetColumn(item.Child, item.Column);
        }
    }
}
