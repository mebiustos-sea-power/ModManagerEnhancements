using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using ModManagerEnhancements;
using ModManagerEnhancements.Core;
using SeaPower;
using Steamworks;

internal static class WorkshopUpdateTests
{
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }

    internal static void Run(Action<string, Action> test)
    {
        test("Workshop snapshots ignore local mods and retain marks through acknowledgement and recreated rows", () =>
        {
            using var f = new Fixture();
            new WorkshopHistory(f.Path, new Dictionary<ulong, uint> { [11] = 100, [22] = 200 }).Acknowledge();
            var first = Source(true, 11); var second = Source(true, 22); var local = Source(false, 11);
            var sources = new[] { first, first, second, local };
            var timestamps = new Dictionary<ulong, uint> { [11] = 101, [22] = 200 };
            int reads = 0;
            uint? Read(ulong id) { reads++; return timestamps[id]; }
            var session = new WorkshopUpdates(sources, f.Path, f.Errors.Add, Read);
            Require(session.HasUnseenUpdates && new WorkshopHistory(f.Path, timestamps).Changed(11),
                "Showing a menu acknowledged or hid an unseen update.");
            timestamps[22] = 201;
            for (int i = 0; i < 5; i++)
            {
                session.Acknowledge();
                Require(!session.HasUnseenUpdates && session.HasUpdates && session.Changed(Source(true, 11)) && !session.Changed(second) && !session.Changed(local),
                    "A visit kept the menu notification, consumed row marks or queried new update information.");
            }
            Require(reads == 2 && f.Errors.Count == 0, "Local/duplicate rows or repeated visits queried Steam.");
            var next = new WorkshopUpdates(sources, f.Path, f.Errors.Add, Read);
            Require(next.HasUnseenUpdates && !next.Changed(first) && next.Changed(second),
                "The next run repeated an acknowledged update or missed a new notification.");
        });
        test("unavailable or failed Workshop reads do not consume updates or block other items", () =>
        {
            using var f = new Fixture();
            new WorkshopHistory(f.Path, new Dictionary<ulong, uint> { [11] = 100, [22] = 200, [33] = 300 }).Acknowledge();
            var first = Source(true, 11); var second = Source(true, 22); var third = Source(true, 33);
            var sources = new[] { first, second, third };
            var session = new WorkshopUpdates(sources, f.Path, f.Errors.Add, id =>
                id == 11 ? 101 : id == 22 ? null : throw new IOException("Read failed."));
            session.Acknowledge();
            Require(session.Changed(first) && !session.Changed(second) && !session.Changed(third) && f.Errors.Count == 1,
                "Unavailable information marked an item, hid another update or suppressed a read error.");
            var recovered = new WorkshopUpdates(sources, f.Path, f.Errors.Add, id => id == 11 ? 101U : id == 22 ? 201U : 301U);
            Require(!recovered.Changed(first) && recovered.Changed(second) && recovered.Changed(third),
                "A previously unavailable update was consumed before it could be observed.");
        });
        test("Workshop history errors are logged without throwing through menu creation or saving fabricated history", () =>
        {
            using var f = new Fixture();
            File.WriteAllText(f.Path, "broken history");
            var source = Source(true, 11);
            var session = new WorkshopUpdates(new[] { source }, f.Path, f.Errors.Add, _ => 101);
            session.Acknowledge();
            Require(f.Errors.Count == 1 && !session.HasUpdates && !session.Changed(source) && File.ReadAllText(f.Path) == "broken history",
                "A history error escaped, disappeared or overwrote the stored data.");
        });
        test("failed Workshop saves clear the viewed menu notice but retain row marks and retryable history", () =>
        {
            using var f = new Fixture();
            new WorkshopHistory(f.Path, new Dictionary<ulong, uint> { [11] = 100 }).Acknowledge();
            var source = Source(true, 11);
            var session = new WorkshopUpdates(new[] { source }, f.Path, f.Errors.Add, _ => 101);
            using (var locked = new FileStream(f.Path, FileMode.Open, FileAccess.Read, FileShare.Read)) session.Acknowledge();
            Require(f.Errors.Count == 1 && !session.HasUnseenUpdates && session.HasUpdates && session.Changed(source),
                "A save error escaped, hid the row marks or kept an already viewed menu notification.");
            var next = new WorkshopUpdates(new[] { source }, f.Path, f.Errors.Add, _ => 101);
            Require(next.HasUnseenUpdates, "A failed save silently consumed the next launch's notification.");
            session.Acknowledge();
            Require(!session.HasUnseenUpdates && session.HasUpdates && !new WorkshopHistory(f.Path, new Dictionary<ulong, uint> { [11] = 101 }).HasUpdates,
                "A later successful save changed the session display or did not acknowledge the update.");
        });
    }

    private static SearchDirectory Source(bool steam, ulong id)
    {
#pragma warning disable SYSLIB0050
        var source = (SearchDirectory)FormatterServices.GetUninitializedObject(typeof(SearchDirectory));
#pragma warning restore SYSLIB0050
        typeof(SearchDirectory).GetField("<IsSteam>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(source, steam);
        source.steamId = new PublishedFileId_t(id);
        return source;
    }
    private sealed class Fixture : IDisposable
    {
        private readonly string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mme-workshop-ui-" + Guid.NewGuid().ToString("N"));
        internal string Path => System.IO.Path.Combine(root, "workshop-updates.txt");
        internal List<Exception> Errors { get; } = new();
        internal Fixture() { Directory.CreateDirectory(root); }
        public void Dispose() { Directory.Delete(root, true); }
    }
}
