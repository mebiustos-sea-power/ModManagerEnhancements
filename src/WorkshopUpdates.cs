using System;
using System.Collections.Generic;
using System.Linq;
using ModManagerEnhancements.Core;
using SeaPower;
using Steamworks;

namespace ModManagerEnhancements;

// Shared for the game process; menus only display or acknowledge this installed snapshot.
internal sealed class WorkshopUpdates
{
    private readonly WorkshopHistory? history;
    private readonly Action<Exception> logError;
    private bool acknowledged;

    internal WorkshopUpdates(IEnumerable<SearchDirectory> sources, string path, Action<Exception> logError,
        Func<ulong, uint?>? readTimestamp = null)
    {
        this.logError = logError;
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
        try { history = new WorkshopHistory(path, installed); }
        catch (Exception error) { logError(error); }
    }

    internal bool HasUpdates => history?.HasUpdates == true;
    internal bool HasUnseenUpdates => HasUpdates && !acknowledged;
    internal bool Changed(SearchDirectory source) => source.IsSteam && history?.Changed(source.steamId.m_PublishedFileId) == true;

    internal void Acknowledge()
    {
        acknowledged = true;
        try { history?.Acknowledge(); }
        catch (Exception error) { logError(error); }
    }

    private static uint? InstalledTimestamp(ulong id) =>
        SteamUGC.GetItemInstallInfo(new PublishedFileId_t(id), out _, out _, 4096, out uint timestamp) ? timestamp : null;
}
