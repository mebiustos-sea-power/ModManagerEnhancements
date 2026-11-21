using System;

namespace ModManagerEnhancements.Core;

internal static class IniRefresh
{
    internal static T Read<T>(string physicalPath, Func<string, T> open, Func<T, string> cacheKey,
        Action<string> invalidate, Action<T> reloadFromDisk)
    {
        T ini = open(physicalPath);
        // Cached reads use a relative key; disk reads and path mappings can use the full path.
        invalidate(cacheKey(ini));
        invalidate(physicalPath);
        reloadFromDisk(ini);
        return ini;
    }
}
