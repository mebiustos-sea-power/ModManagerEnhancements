using System;
using System.Collections.Generic;
using System.Linq;
using ModManagerEnhancements.Core;
using Noesis;

namespace ModManagerEnhancements;

internal sealed class SourceIndicator : IDisposable
{
    private readonly ColumnDefinition column;
    private readonly GridLength originalWidth;
    private readonly Button source;
    private readonly List<SavedProperty> saved = new();
    private float iconSize;

    internal SourceIndicator(Grid row)
    {
        column = row.ColumnDefinitions[8]; originalWidth = column.Width;
        source = row.Children.OfType<Button>().SingleOrDefault(child => Grid.GetColumn(child) == 8)
            ?? throw new InvalidOperationException("The native source button is missing from the source column.");
        try
        {
            // Both origins use one native button; keep its caption, IsSteam binding and command intact.
            Save(ContentControl.ContentTemplateProperty); Save(FrameworkElement.ToolTipProperty);
            Save(FrameworkElement.WidthProperty); Save(FrameworkElement.MinWidthProperty);
            Save(FrameworkElement.MarginProperty); Save(Control.PaddingProperty);
            Save(Control.HorizontalContentAlignmentProperty); Save(FrameworkElement.HorizontalAlignmentProperty);
            Save(ToolTipService.ShowOnDisabledProperty);
            source.Resources[SourceIcons.SteamResource] = SourceIcons.Steam;
            source.Resources[SourceIcons.SizeResource] = 16f;
            source.MinWidth = 0; source.Padding = new Thickness(4, 1, 4, 1); source.Margin = new Thickness(4, 0, 4, 0);
            source.HorizontalAlignment = HorizontalAlignment.Center; source.HorizontalContentAlignment = HorizontalAlignment.Center;
            // Local entries are disabled by the game but still need their origin tooltip.
            ToolTipService.SetShowOnDisabled(source, true);
        }
        catch { Dispose(); throw; }
    }

    internal void Update(RowMetrics metrics, ModText text, bool isSteam)
    {
        if (iconSize != metrics.IconSize)
        { iconSize = metrics.IconSize; source.Resources[SourceIcons.SizeResource] = iconSize; }
        source.Width = metrics.IconSize + 8; column.Width = new GridLength(metrics.SourceColumnWidth);
        source.ContentTemplate = SourceIcons.Template(isSteam);
        source.ToolTip = isSteam ? text.WorkshopTooltip : text.LocalTooltip;
    }

    private void Save(DependencyProperty property) => saved.Add(new SavedProperty(source, property));

    public void Dispose()
    {
        for (int i = saved.Count - 1; i >= 0; i--) saved[i].Restore();
        saved.Clear(); column.Width = originalWidth;
        source.Resources.Remove(SourceIcons.SteamResource); source.Resources.Remove(SourceIcons.SizeResource);
    }

    private sealed class SavedProperty
    {
        private readonly DependencyObject target;
        private readonly DependencyProperty property;
        private readonly BindingBase? binding;
        private readonly object value;
        internal SavedProperty(DependencyObject target, DependencyProperty property)
        {
            this.target = target; this.property = property;
            binding = BindingOperations.GetBindingBase(target, property);
            value = target.ReadLocalValue(property);
        }
        internal void Restore()
        {
            if (binding != null) BindingOperations.SetBinding(target, property, binding);
            else if (ReferenceEquals(value, DependencyProperty.UnsetValue)) target.ClearValue(property);
            else target.SetValue(property, value);
        }
    }
}
