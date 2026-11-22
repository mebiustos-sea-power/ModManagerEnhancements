using System;
using System.Collections.Generic;
using System.Linq;
using ModManagerEnhancements.Core;
using SeaPower;
using Steamworks;

namespace ModManagerEnhancements;

// One snapshot per opening; row refreshes and search never consume the visible marks.
internal sealed class WorkshopUpdates
{
    private readonly HashSet<ulong> changed = new();

    internal WorkshopUpdates(IEnumerable<SearchDirectory> sources, string path, Action<Exception> logError,
        Func<ulong, uint?>? readTimestamp = null)
    {
        readTimestamp ??= InstalledTimestamp;
        var installed = new Dictionary<ulong, uint>();
        foreach (ulong id in sources.Where(source => source.IsSteam).Select(source => source.steamId.m_PublishedFileId).Distinct())
        {
            try
            {
                uint? timestamp = readTimestamp(id);
                if (timestamp.HasValue && timestamp.Value != 0) installed.Add(id, timestamp.Value);
            }
            catch (Exception error) { logError(error); }
        }
        try { changed = WorkshopHistory.Open(path, installed); }
        catch (Exception error) { logError(error); }
    }

    internal bool Changed(SearchDirectory source) => source.IsSteam && changed.Contains(source.steamId.m_PublishedFileId);

    private static uint? InstalledTimestamp(ulong id) =>
        SteamUGC.GetItemInstallInfo(new PublishedFileId_t(id), out _, out _, 4096, out uint timestamp) ? timestamp : null;
}
