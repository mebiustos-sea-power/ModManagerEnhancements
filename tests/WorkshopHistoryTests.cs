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

    internal static void Run(Action<string, Action> test)
    {
        test("Workshop history baselines the first visit and consumes changes only on the next opening", () =>
        {
            using var f = new Fixture();
            Require(WorkshopHistory.Open(f.Path, new Dictionary<ulong, uint>()).Count == 0 && !File.Exists(f.Path),
                "An empty list created a comparison baseline.");
            var installed = new Dictionary<ulong, uint> { [11] = 100, [22] = 200 };
            Require(WorkshopHistory.Open(f.Path, installed).Count == 0, "The first visit marked existing items as updated.");
            installed[11] = 101;
            var visit = WorkshopHistory.Open(f.Path, installed);
            Require(visit.SetEquals(new[] { 11UL }), "An unchanged item was marked or an updated item was missed.");
            Require(WorkshopHistory.Open(f.Path, installed).Count == 0, "The next opening repeated an already observed update.");
        });
        test("new Workshop subscriptions establish a baseline while changed timestamps include older revisions", () =>
        {
            using var f = new Fixture();
            WorkshopHistory.Open(f.Path, new Dictionary<ulong, uint> { [11] = 100 });
            var changed = WorkshopHistory.Open(f.Path, new Dictionary<ulong, uint> { [11] = 90, [ulong.MaxValue] = 300 });
            Require(changed.SetEquals(new[] { 11UL }), "New subscriptions looked updated or a changed revision was ignored.");
            Require(WorkshopHistory.Open(f.Path, new Dictionary<ulong, uint> { [ulong.MaxValue] = 301 }).SetEquals(new[] { ulong.MaxValue }),
                "A persisted Workshop ID lost its identity.");
        });
        test("temporarily unavailable Workshop information keeps its previous comparison point", () =>
        {
            using var f = new Fixture();
            WorkshopHistory.Open(f.Path, new Dictionary<ulong, uint> { [11] = 100, [22] = 200 });
            Require(WorkshopHistory.Open(f.Path, new Dictionary<ulong, uint> { [11] = 101 }).SetEquals(new[] { 11UL }),
                "An available update was missed.");
            WorkshopHistory.Open(f.Path, new Dictionary<ulong, uint>());
            Require(WorkshopHistory.Open(f.Path, new Dictionary<ulong, uint> { [22] = 201 }).SetEquals(new[] { 22UL }),
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
                Fails(() => WorkshopHistory.Open(f.Path, new Dictionary<ulong, uint> { [11] = 200 }));
                Require(File.ReadAllBytes(f.Path).SequenceEqual(before), "Invalid history was replaced by a fabricated baseline.");
            }
        });
        test("a failed Workshop history save leaves previous observations and no temporary files", () =>
        {
            using var f = new Fixture();
            WorkshopHistory.Open(f.Path, new Dictionary<ulong, uint> { [11] = 100 });
            byte[] before = File.ReadAllBytes(f.Path);
            using (var locked = new FileStream(f.Path, FileMode.Open, FileAccess.Read, FileShare.Read))
                Fails(() => WorkshopHistory.Open(f.Path, new Dictionary<ulong, uint> { [11] = 101 }));
            Require(File.ReadAllBytes(f.Path).SequenceEqual(before) &&
                Directory.GetFiles(System.IO.Path.GetDirectoryName(f.Path)!).Length == 1, "A failed save consumed history or left a partial file.");
            Require(WorkshopHistory.Open(f.Path, new Dictionary<ulong, uint> { [11] = 101 }).Contains(11), "Retry lost the update.");
        });
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mme-workshop-history-" + Guid.NewGuid().ToString("N"));
        internal string Path => System.IO.Path.Combine(root, "state", "workshop-updates.txt");
        public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
