using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using ModManagerEnhancements.Core;

internal static class Program
{
    private static int passed;
    private static readonly ConflictScanner Scanner = new();
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static ConflictReport Scan(params ModSource[] mods) => Scanner.Scan(mods, CancellationToken.None);
    private static void Test(string name, Action test) { test(); passed++; Console.WriteLine("PASS " + name); }

    private sealed class Fixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "ModManagerEnhancements-tests-" + Guid.NewGuid().ToString("N"));
        internal ModSource Source(string name, bool enabled = true, bool baseGame = false)
        {
            string folder = Path.Combine(root, name); Directory.CreateDirectory(folder);
            return new ModSource(folder, name, folder, enabled, baseGame);
        }
        internal void Write(ModSource source, string relative, string contents)
        {
            string path = Path.Combine(source.Directory, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, contents);
        }
        public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length > 0 && args[0] == "--scan-installed") { ScanInstalled(args); return 0; }
            Test("priority follows current order in both directions", () =>
            {
                using var f = new Fixture(); var a = f.Source("A"); var b = f.Source("B");
                f.Write(a, "ui/image.png", "aaa"); f.Write(b, "ui/image.png", "bbb");
                var first = Scan(a, b); var reversed = Scan(b, a);
                Require(first.Files.Single().Preferred.Mod.Id == a.Id && reversed.Files.Single().Preferred.Mod.Id == b.Id, "Wrong file provider after reorder.");
                Require(first.Impact(a.Id).Preferred == 1 && first.Impact(b.Id).Overridden == 1, "Wrong row counts.");
            });
            Test("disabling a provider removes its overlaps", () =>
            {
                using var f = new Fixture(); var a = f.Source("A"); var b = f.Source("B", false);
                f.Write(a, "image.png", "aaa"); f.Write(b, "image.png", "bbb");
                Require(Scan(a, b).Files.Count == 0, "Disabled mod contributed a conflict.");
            });
            Test("base game replacements are excluded", () =>
            {
                using var f = new Fixture(); var a = f.Source("A"); var original = f.Source("original", true, true);
                f.Write(a, "image.png", "aaa"); f.Write(original, "image.png", "bbb");
                Require(Scan(a, original).Files.Count == 0, "Normal base-game override was marked as a mod conflict.");
            });
            Test("user data can override another mod", () =>
            {
                using var f = new Fixture(); var user = f.Source("UserData"); var a = f.Source("A");
                f.Write(user, "image.png", "user"); f.Write(a, "image.png", "mod");
                Require(Scan(user, a).Files.Single().Preferred.Mod.Id == user.Id, "User Data priority was lost.");
            });
            Test("identical contents do not produce badges", () =>
            {
                using var f = new Fixture(); var a = f.Source("A"); var b = f.Source("B");
                f.Write(a, "image.png", "same"); f.Write(b, "image.png", "same");
                Require(Scan(a, b).Files.Count == 0, "Identical content was marked.");
            });
            Test("same size and timestamp do not hide changed contents", () =>
            {
                using var f = new Fixture(); var a = f.Source("A"); var b = f.Source("B");
                f.Write(a, "image.png", "aaaa"); f.Write(b, "image.png", "bbbb");
                var stamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                File.SetLastWriteTimeUtc(Path.Combine(a.Directory, "image.png"), stamp);
                File.SetLastWriteTimeUtc(Path.Combine(b.Directory, "image.png"), stamp);
                Require(Scan(a, b).Files.Count == 1, "Size/timestamp equality was mistaken for identical contents.");
            });
            Test("all lower providers remain visible", () =>
            {
                using var f = new Fixture(); var a = f.Source("A"); var b = f.Source("B"); var c = f.Source("C");
                f.Write(a, "image.png", "aaa"); f.Write(b, "image.png", "bbb"); f.Write(c, "image.png", "ccc");
                var result = Scan(a, b, c);
                Require(result.Files.Single().Providers.Count == 3 && result.Impact(b.Id).Overridden == 1 && result.Impact(c.Id).Overridden == 1, "Multi-provider overlap was incomplete.");
            });
            Test("a lower identical copy is not falsely marked as losing content", () =>
            {
                using var f = new Fixture(); var a = f.Source("A"); var b = f.Source("B"); var c = f.Source("C");
                f.Write(a, "image.png", "same"); f.Write(b, "image.png", "same"); f.Write(c, "image.png", "different");
                var result = Scan(a, b, c);
                Require(!result.Impact(b.Id).HasOverlaps && result.Files.Single().Providers.Count == 3, "Identical intermediate provider was misclassified.");
            });
            Test("one mod can have both preferred and overridden files", () =>
            {
                using var f = new Fixture(); var a = f.Source("A"); var b = f.Source("B"); var c = f.Source("C");
                f.Write(a, "a.png", "a"); f.Write(b, "a.png", "b"); f.Write(b, "b.png", "b"); f.Write(c, "b.png", "c");
                var impact = Scan(a, b, c).Impact(b.Id);
                Require(impact.Preferred == 1 && impact.Overridden == 1, "Mixed-direction badge counts were wrong.");
            });
            Test("relative paths match case insensitively", () =>
            {
                using var f = new Fixture(); var a = f.Source("A"); var b = f.Source("B");
                f.Write(a, "UI/Image.PNG", "a"); f.Write(b, "ui/image.png", "b");
                Require(Scan(a, b).Files.Count == 1, "Windows path casing prevented overlap detection.");
            });
            Test("package metadata and DLLs are not file override conflicts", () =>
            {
                using var f = new Fixture(); var a = f.Source("A"); var b = f.Source("B");
                foreach (string name in new[] { "_info.ini", "thumbnail.png", "README.md", "LICENSE", "Plugin.dll", "Plugin.pdb" })
                { f.Write(a, name, "a"); f.Write(b, name, "b"); }
                Require(Scan(a, b).Files.Count == 0, "Package metadata or code binaries created a false file override.");
            });
            Test("rescan observes edits additions and deletions", () =>
            {
                using var f = new Fixture(); var a = f.Source("A"); var b = f.Source("B");
                f.Write(a, "image.png", "a"); f.Write(b, "image.png", "a");
                Require(Scan(a, b).Files.Count == 0, "Initial state wrong.");
                f.Write(b, "image.png", "b"); Require(Scan(a, b).Files.Count == 1, "Edited contents were cached indefinitely.");
                f.Write(a, "new.png", "a"); f.Write(b, "new.png", "b"); Require(Scan(a, b).Files.Count == 2, "New file not observed.");
                File.Delete(Path.Combine(b.Directory, "image.png")); Require(Scan(a, b).Files.Count == 1, "Deleted file remained in report.");
            });
            Test("details filter to the selected mod and qualify INI files", () =>
            {
                using var f = new Fixture(); var a = f.Source("A"); var b = f.Source("B"); var c = f.Source("C");
                f.Write(a, "language/names.ini", "a"); f.Write(b, "language/names.ini", "b"); f.Write(b, "ui/b.png", "b"); f.Write(c, "ui/b.png", "c");
                var report = Scan(a, b, c);
                var model = new DetailsModel(new ModText("ja"), report, a.Id, a.Name, null);
                Require(model.Files.Count == 1 && model.Files[0].Path == "language/names.ini" && model.Files[0].Note.Length > 0, "Selected-mod details or INI qualification missing.");
                Require(model.Files[0].Locations.Contains(Path.Combine(a.Directory, "language", "names.ini")), "Source path unavailable for inspection.");
                Require(model.IsFiltered && model.Selection.Contains(a.Name), "Filtered view did not identify its scope or expose the reset action.");
                foreach (string language in new[] { "ja", "en" })
                {
                    var text = new ModText(language);
                    var all = new DetailsModel(text, report, null, null, null);
                    Require(!all.IsFiltered && all.Selection.Length == 0 && all.Files.Count == 2,
                        "Full view must show every file without a redundant scope label or reset action.");
                    var filtered = new DetailsModel(text, report, a.Id, a.Name, null);
                    Require(filtered.IsFiltered && filtered.Selection.Contains(a.Name) && filtered.Files.Count == 1,
                        "Selecting a mod must expose its scope and the action to return to all files.");
                    var scanning = new DetailsModel(text, null, a.Id, a.Name, null);
                    Require(scanning.IsFiltered && scanning.Selection == filtered.Selection,
                        "Rescanning must preserve the selected scope and reset action.");
                    var reset = new DetailsModel(text, report, null, null, null);
                    Require(!reset.IsFiltered && reset.Selection.Length == 0 && reset.Files.Count == all.Files.Count,
                        "Clearing the filter must restore all files and hide the reset action.");
                }
            });
            Test("missing directories fail instead of reporting no conflicts", () =>
            {
                using var f = new Fixture(); var a = f.Source("A"); Directory.Delete(a.Directory);
                try { Scan(a); throw new Exception("Missing directory silently accepted."); }
                catch (DirectoryNotFoundException) { }
            });
            Test("canceled scans stop", () =>
            {
                using var c = new CancellationTokenSource(); c.Cancel();
                try { Scanner.Scan(new[] { new ModSource("a", "A", "unused") }, c.Token); throw new Exception("Canceled scan continued."); }
                catch (OperationCanceledException) { }
            });
            Test("latest selection wins when an older scan finishes later", () =>
            {
                using var started = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
                var old = new ConflictReport(Array.Empty<FileOverlap>()); var latest = new ConflictReport(Array.Empty<FileOverlap>());
                using var session = new ScanSession((mods, token) =>
                {
                    if (mods[0].Id == "old") { started.Set(); if (!release.Wait(5000)) throw new Exception("Test worker timed out."); return old; }
                    return latest;
                }, error => throw new Exception("Unexpected worker error.", error));
                session.Request(new[] { new ModSource("old", "old", "unused") }); Require(started.Wait(5000), "Old worker did not start.");
                session.Request(new[] { new ModSource("new", "new", "unused") });
                ScanOutcome? accepted = null;
                Require(SpinWait.SpinUntil(() => { accepted = session.Poll() ?? accepted; return accepted != null; }, 5000), "Current result did not arrive.");
                Require(ReferenceEquals(accepted!.Report, latest), "Old result overwrote current selection.");
                release.Set();
                bool stalePublished = false;
                Require(SpinWait.SpinUntil(() => { if (session.Poll() != null) stalePublished = true; return !session.HasPending; }, 5000), "Old worker did not finish.");
                Require(!stalePublished, "Stale result was published after the latest result.");
            });
            Test("scan failure is reported and a later rescan can recover", () =>
            {
                int errors = 0;
                using var session = new ScanSession((mods, token) => mods[0].Id == "bad" ? throw new IOException("Unreadable mod file") : new ConflictReport(Array.Empty<FileOverlap>()), error => errors++);
                session.Request(new[] { new ModSource("bad", "bad", "unused") });
                ScanOutcome? outcome = null;
                Require(SpinWait.SpinUntil(() => (outcome = session.Poll()) != null, 5000), "Failure not delivered.");
                Require(outcome!.Error is IOException && outcome.Report == null && errors == 1, "Failure was disguised as a normal report.");
                session.Request(new[] { new ModSource("good", "good", "unused") }); outcome = null;
                Require(SpinWait.SpinUntil(() => (outcome = session.Poll()) != null, 5000) && outcome!.Report != null && outcome.Error == null, "Recovery failed.");
            });
            Test("disposed sessions reject further work", () =>
            {
                var session = new ScanSession((mods, token) => new ConflictReport(Array.Empty<FileOverlap>()), _ => { }); session.Dispose();
                try { session.Request(Array.Empty<ModSource>()); throw new Exception("Disposed session accepted work."); }
                catch (ObjectDisposedException) { }
            });
            Console.WriteLine($"Passed {passed} functional checks."); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void ScanInstalled(string[] args)
    {
        if (args.Length != 4) throw new ArgumentException("Use --scan-installed <usersettings.ini> <StreamingAssets> <WorkshopRoot>");
        var mods = new List<ModSource>(); bool loadOrder = false;
        foreach (string raw in File.ReadAllLines(args[1]))
        {
            string line = raw.Trim();
            if (line.StartsWith("[", StringComparison.Ordinal)) { loadOrder = line == "[LoadOrder]"; continue; }
            if (!loadOrder || !line.StartsWith("Mod", StringComparison.Ordinal) || !line.Contains("=")) continue;
            string[] parts = line.Substring(line.IndexOf('=') + 1).Split(',');
            if (parts.Length != 3 || !bool.TryParse(parts[1], out bool enabled)) throw new FormatException("Invalid mod setting: " + line);
            string path = Path.Combine(parts[2] == "0" ? args[2] : args[3], parts[0]);
            mods.Add(new ModSource(path, parts[0], path, enabled));
        }
        var report = Scanner.Scan(mods, CancellationToken.None);
        Console.WriteLine($"Installed-mod scan: {mods.Count} entries, {report.Files.Count} differing shared paths.");
        foreach (var file in report.Files) Console.WriteLine(file.RelativePath + " : " + string.Join(" > ", file.Providers.Select(p => p.Mod.Name)));
    }
}
