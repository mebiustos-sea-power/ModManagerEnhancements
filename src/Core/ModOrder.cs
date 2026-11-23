using System;
using System.Collections.Generic;
using System.Linq;

namespace ModManagerEnhancements.Core;

internal static class ModOrder
{
    // Base Game is a fixed final layer, outside the enabled and disabled mod groups.
    internal static T[] EnabledFirst<T>(IEnumerable<T> mods, Func<T, bool> enabled, Func<T, bool> isBaseGame) =>
        mods.OrderBy(mod => isBaseGame(mod) ? 2 : enabled(mod) ? 0 : 1).ToArray();
}
