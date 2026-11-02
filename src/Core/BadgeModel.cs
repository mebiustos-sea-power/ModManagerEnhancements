namespace ModManagerEnhancements.Core;

internal sealed class BadgeModel
{
    internal ModImpact Impact { get; }
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
