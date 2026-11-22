using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModManagerEnhancements.Core;

internal static class WorkshopHistoryTests
{
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Fails(Action action)
    {
        bool failed = false;
        try { action(); } catch (Exception error) when (error is IOException || error is InvalidDataException) { failed = true; }
        Require(failed, "A failed history operation was silently accepted.");
    }
    private static WorkshopHistory Seen(string path, IReadOnlyDictionary<ulong, uint> installed)
    {
        var history = new WorkshopHistory(path, installed); history.Acknowledge(); return history;
    }

    internal static void Run(Action<string, Action> test)
    {
        test("Workshop detection is read-only and acknowledgement retains this run's marks", () =>
        {
            using var f = new Fixture();
            var empty = new WorkshopHistory(f.Path, new Dictionary<ulong, uint>()); empty.Acknowledge();
            Require(!empty.HasUpdates && !File.Exists(f.Path), "An empty list created a comparison baseline.");
            var installed = new Dictionary<ulong, uint> { [11] = 100, [22] = 200 };
            var first = new WorkshopHistory(f.Path, installed);
            Require(!first.HasUpdates && !File.Exists(f.Path), "Detection marked the first use or saved it without a visit.");
            first.Acknowledge();
            byte[] before = File.ReadAllBytes(f.Path);
            installed[11] = 101;
            var session = new WorkshopHistory(f.Path, installed);
            Require(session.HasUpdates && session.Changed(11) && !session.Changed(22), "Updated/unchanged items were misidentified.");
            Require(File.ReadAllBytes(f.Path).SequenceEqual(before) && new WorkshopHistory(f.Path, installed).Changed(11),
                "Leaving the main menu without a visit consumed the next launch's update.");
            session.Acknowledge();
            using (var locked = new FileStream(f.Path, FileMode.Open, FileAccess.Read, FileShare.Read))
                session.Acknowledge();
            Require(session.HasUpdates && session.Changed(11) && !new WorkshopHistory(f.Path, installed).HasUpdates,
                "Acknowledging/reopening cleared this run's marks or repeated them on the next launch.");
        });
        test("Workshop observations remain frozen even if the caller changes its timestamp collection", () =>
        {
            using var f = new Fixture();
            Seen(f.Path, new Dictionary<ulong, uint> { [11] = 100, [22] = 200 });
            var installed = new Dictionary<ulong, uint> { [11] = 101, [22] = 200 };
            var session = new WorkshopHistory(f.Path, installed);
            installed[11] = 102; installed[22] = 201;
            session.Acknowledge();
            Require(session.Changed(11) && !session.Changed(22), "The visible snapshot changed after detection.");
            var next = new WorkshopHistory(f.Path, installed);
            Require(next.Changed(11) && next.Changed(22), "Saving recorded timestamps that were never observed.");
        });
        test("new Workshop subscriptions establish a baseline while changed timestamps include older revisions", () =>
        {
            using var f = new Fixture();
            Seen(f.Path, new Dictionary<ulong, uint> { [11] = 100 });
            var changed = Seen(f.Path, new Dictionary<ulong, uint> { [11] = 90, [ulong.MaxValue] = 300 });
            Require(changed.Changed(11) && !changed.Changed(ulong.MaxValue), "New subscriptions looked updated or an older revision was ignored.");
            Require(new WorkshopHistory(f.Path, new Dictionary<ulong, uint> { [ulong.MaxValue] = 301 }).Changed(ulong.MaxValue),
                "A persisted Workshop ID lost its identity.");
        });
        test("temporarily unavailable Workshop information keeps its previous comparison point", () =>
        {
            using var f = new Fixture();
            Seen(f.Path, new Dictionary<ulong, uint> { [11] = 100, [22] = 200 });
            Require(Seen(f.Path, new Dictionary<ulong, uint> { [11] = 101 }).Changed(11), "An available update was missed.");
            Seen(f.Path, new Dictionary<ulong, uint>());
            Require(new WorkshopHistory(f.Path, new Dictionary<ulong, uint> { [22] = 201 }).Changed(22),
                "Unavailable information erased or consumed an unobserved update.");
        });
        test("invalid Workshop history is reported without overwriting its stored contents", () =>
        {
            using var f = new Fixture();
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(f.Path)!);
            foreach (string invalid in new[] { "not history", "11=100\n11=101\n", "0=100\n", "11=0\n" })
            {
                File.WriteAllText(f.Path, invalid);
                byte[] before = File.ReadAllBytes(f.Path);
                Fails(() => new WorkshopHistory(f.Path, new Dictionary<ulong, uint> { [11] = 200 }));
                Require(File.ReadAllBytes(f.Path).SequenceEqual(before), "Invalid history was replaced by a fabricated baseline.");
            }
        });
        test("a failed Workshop acknowledgement retains visible changes and allows a later visit to retry", () =>
        {
            using var f = new Fixture();
            Seen(f.Path, new Dictionary<ulong, uint> { [11] = 100 });
            byte[] before = File.ReadAllBytes(f.Path);
            var installed = new Dictionary<ulong, uint> { [11] = 101 };
            var session = new WorkshopHistory(f.Path, installed);
            using (var locked = new FileStream(f.Path, FileMode.Open, FileAccess.Read, FileShare.Read))
                Fails(session.Acknowledge);
            Require(session.Changed(11) && File.ReadAllBytes(f.Path).SequenceEqual(before) &&
                Directory.GetFiles(System.IO.Path.GetDirectoryName(f.Path)!).Length == 1,
                "A failed save consumed an update or left a partial file.");
            Require(new WorkshopHistory(f.Path, installed).Changed(11), "A failed acknowledgement consumed the next launch's update.");
            session.Acknowledge();
            Require(session.Changed(11) && !new WorkshopHistory(f.Path, installed).HasUpdates, "Retry lost visible changes or failed to save them.");
        });
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mme-workshop-history-" + Guid.NewGuid().ToString("N"));
        internal string Path => System.IO.Path.Combine(root, "state", "workshop-updates.txt");
        public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
