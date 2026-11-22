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
        test("Workshop update snapshots ignore local mods and retain marks across repeated row queries", () =>
        {
            using var f = new Fixture();
            WorkshopHistory.Open(f.Path, new Dictionary<ulong, uint> { [11] = 100, [22] = 200 });
            var first = Source(true, 11); var second = Source(true, 22); var local = Source(false, 11);
            var sources = new[] { first, first, second, local };
            var timestamps = new Dictionary<ulong, uint> { [11] = 101, [22] = 200 };
            int reads = 0;
            uint? Read(ulong id) { reads++; return timestamps[id]; }
            var visit = new WorkshopUpdates(sources, f.Path, f.Errors.Add, Read);
            timestamps[22] = 201;
            for (int i = 0; i < 5; i++)
                Require(visit.Changed(first) && !visit.Changed(second) && !visit.Changed(local), "Refreshing rows consumed marks or queried new update information.");
            Require(reads == 2 && f.Errors.Count == 0, "Local or duplicate rows queried Steam, or a normal visit failed.");
            timestamps[22] = 200;
            var next = new WorkshopUpdates(sources, f.Path, f.Errors.Add, Read);
            Require(!next.Changed(first) && !next.Changed(second), "Closing and reopening repeated consumed updates.");
        });
        test("unavailable or failed Workshop reads do not consume updates or block other items", () =>
        {
            using var f = new Fixture();
            WorkshopHistory.Open(f.Path, new Dictionary<ulong, uint> { [11] = 100, [22] = 200, [33] = 300 });
            var first = Source(true, 11); var second = Source(true, 22); var third = Source(true, 33);
            var sources = new[] { first, second, third };
            var visit = new WorkshopUpdates(sources, f.Path, f.Errors.Add, id =>
                id == 11 ? 101 : id == 22 ? null : throw new IOException("Read failed."));
            Require(visit.Changed(first) && !visit.Changed(second) && !visit.Changed(third) && f.Errors.Count == 1,
                "Unavailable information marked an item, hid another update or suppressed a read error.");
            var recovered = new WorkshopUpdates(sources, f.Path, f.Errors.Add, id => id == 11 ? 101U : id == 22 ? 201U : 301U);
            Require(!recovered.Changed(first) && recovered.Changed(second) && recovered.Changed(third),
                "A previously unavailable update was consumed before it could be observed.");
        });
        test("Workshop history errors are logged without throwing through the manager mount", () =>
        {
            using var f = new Fixture();
            File.WriteAllText(f.Path, "broken history");
            var source = Source(true, 11);
            var visit = new WorkshopUpdates(new[] { source }, f.Path, f.Errors.Add, _ => 101);
            Require(f.Errors.Count == 1 && !visit.Changed(source) && File.ReadAllText(f.Path) == "broken history",
                "A history error escaped, disappeared or overwrote the stored data.");
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
