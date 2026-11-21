using ModManagerEnhancements.Core;
using SeaPower;

namespace ModManagerEnhancements;

internal readonly struct CompatibilityRow
{
    internal SearchDirectory Source { get; }
    internal bool OutOfRange { get; }
    internal CompatibilityOverrideState Override { get; }
    internal bool Modified => Override == CompatibilityOverrideState.Modified;
    internal bool CanRestore => Modified;
    internal CompatibilityRow(SearchDirectory source, bool outOfRange, CompatibilityOverrideState state)
    { Source = source; OutOfRange = outOfRange; Override = state; }
}
