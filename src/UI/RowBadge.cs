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
    private readonly ColumnDefinition column;
    private readonly Action<SearchDirectory> open;
    private readonly (UIElement Child, int Column)[] shifted;

    internal RowBadge(Grid row, SearchDirectory source, Action<SearchDirectory> open)
    {
        this.row = row; Source = source; this.open = open;
        if (row.ColumnDefinitions.Count < 9) throw new InvalidOperationException("The mod row no longer has the expected grid layout.");
        column = new ColumnDefinition { Width = GridLength.Auto };
        shifted = row.Children.Cast<UIElement>().Where(c => Grid.GetColumn(c) >= 4).Select(c => (c, Grid.GetColumn(c))).ToArray();
        button = new Button { Padding = new Thickness(4, 1, 4, 1), Margin = new Thickness(4, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 217, 183, 111)) };
        button.Style = row.Children.OfType<Button>().Select(b => b.Style).FirstOrDefault(s => s != null);
        button.Click += Click;
        // Add one column before the existing status columns without replacing the game's template.
        try
        {
            row.ColumnDefinitions.Insert(4, column);
            foreach (var item in shifted) Grid.SetColumn(item.Child, item.Column + 1);
            Grid.SetColumn(button, 4);
            row.Children.Add(button);
        }
        catch { Dispose(); throw; }
    }

    internal void Update(ConflictReport? report, ModText text)
    {
        var impact = report?.Impact(Source.DirectoryInfo.FullName) ?? default;
        button.Content = text.Badge(impact);
        button.ToolTip = text.Tooltip(impact);
        button.Visibility = impact.HasOverlaps ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Click(object sender, RoutedEventArgs args)
    { args.Handled = true; Plugin.Guard(() => open(Source)); }

    public void Dispose()
    {
        button.Click -= Click;
        row.Children.Remove(button);
        if (row.ColumnDefinitions.Contains(column))
        {
            row.ColumnDefinitions.Remove(column);
            foreach (var item in shifted) if (row.Children.Contains(item.Child)) Grid.SetColumn(item.Child, item.Column);
        }
    }
}
