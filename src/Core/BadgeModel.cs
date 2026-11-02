namespace ModManagerEnhancements.Core;

internal sealed class BadgeModel
{
    internal ModImpact Impact { get; }
    internal string PreferredText => Count("↑", Impact.Preferred);
    internal string OverriddenText => Count("↓", Impact.Overridden);
    private static string Count(string arrow, int count) => count == 0 ? "" : arrow + (count > 9 ? "9+" : count.ToString(System.Globalization.CultureInfo.InvariantCulture));
    internal bool IsSelected { get; }
    internal bool HighlightPreferred { get; }
    internal bool HighlightOverridden { get; }

    internal BadgeModel(ConflictReport? report, string modId, string? selectedId)
    {
        Impact = report?.Impact(modId) ?? default;
        if (report == null || selectedId == null || !Impact.HasOverlaps) return;
        IsSelected = ConflictReport.SameId(modId, selectedId);
        var related = report.Impact(modId, selectedId);
        HighlightPreferred = related.Preferred > 0;
        HighlightOverridden = related.Overridden > 0;
    }
}
