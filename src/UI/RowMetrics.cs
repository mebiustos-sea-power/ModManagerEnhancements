using System;
using Noesis;

namespace ModManagerEnhancements;

// One set of font-derived dimensions is shared by all rows in the manager.
internal sealed class RowMetrics
{
    private FontFamily? family;
    private float size;
    private FontWeight weight;
    private FontStyle style;
    private FontStretch stretch;
    internal float DirectionWidth { get; private set; }
    internal float IconSize { get; private set; }
    internal float ButtonWidth => DirectionWidth * 2 + 4 + 14;
    internal float OverrideColumnWidth => ButtonWidth + 8;
    internal float SourceColumnWidth => IconSize + 16;

    internal void Update(TextBlock sample)
    {
        if (family == sample.FontFamily && size == sample.FontSize && weight == sample.FontWeight
            && style == sample.FontStyle && stretch == sample.FontStretch) return;
        family = sample.FontFamily; size = sample.FontSize; weight = sample.FontWeight;
        style = sample.FontStyle; stretch = sample.FontStretch;
        float width = 0;
        foreach (string arrow in new[] { "↑", "↓" })
            foreach (string count in new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "9+" })
            {
                var probe = new TextBlock { Text = arrow + count, FontFamily = family, FontSize = size,
                    FontWeight = weight, FontStyle = style, FontStretch = stretch };
                probe.Measure(new Size(float.PositiveInfinity, float.PositiveInfinity));
                width = Math.Max(width, probe.DesiredSize.Width);
            }
        DirectionWidth = (float)Math.Ceiling(width);
        IconSize = size;
    }
}
