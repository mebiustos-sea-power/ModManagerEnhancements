using System;
using System.IO;
using System.Threading;
using ModManagerEnhancements.Core;

internal static class DllTests
{
    private static readonly ModContentScanner Scanner = new();
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static ModContentInventory Scan(params ModSource[] mods) => Scanner.Scan(mods, CancellationToken.None);

    internal static void Run(Action<string, Action> test)
    {
        test("DLL detection includes disabled local and Workshop folders without metadata", () =>
        {
            using var f = new Program.Fixture(); var local = f.Source("Local", false); var workshop = f.Source("Workshop"); var data = f.Source("Data");
            f.Write(local, "Plugin.dll", "not an executable assembly");
            f.Write(workshop, "libraries/Helper.DLL", "not an executable assembly"); f.Write(data, "file.ini", "data");
            var inventory = Scan(local, workshop, data);
            Require(inventory.ContainsDll(local.Id.ToUpperInvariant()) && inventory.ContainsDll(workshop.Id) && !inventory.ContainsDll(data.Id),
                "Physical DLL presence must not depend on enable state, Workshop metadata, or ID casing.");
        });
        test("DLL detection rejects similar names and excludes the base game", () =>
        {
            using var f = new Program.Fixture(); var data = f.Source("Data"); var original = f.Source("original", true, true);
            f.Write(data, "backup.dll.disabled", "backup"); f.Write(data, "folder.dll/file.txt", "not a DLL file"); f.Write(data, "Plugin.pdb", "symbols");
            f.Write(original, "Base.dll", "base game");
            var inventory = Scan(data, original);
            Require(!inventory.ContainsDll(data.Id) && !inventory.ContainsDll(original.Id), "A non-DLL file, directory name or base-game file created a DLL badge.");
        });
        test("DLL inspection covers hidden subfolders without opening or locking assemblies", () =>
        {
            using var f = new Program.Fixture(); var source = f.Source("Source");
            f.Write(source, ".hidden/nested/Native.dll", "invalid and locked binary");
            using var locked = new FileStream(Path.Combine(source.Directory, ".hidden", "nested", "Native.dll"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Require(Scan(source).ContainsDll(source.Id), "DLL inspection read or loaded assembly contents, or missed a nested DLL.");
        });
        test("DLL rescan observes additions and removals independently of enable state", () =>
        {
            using var f = new Program.Fixture(); var source = f.Source("Source", false);
            Require(!Scan(source).ContainsDll(source.Id), "An empty mod acquired a DLL badge.");
            f.Write(source, "plugins/New.dll", "new");
            Require(Scan(source).ContainsDll(source.Id), "A newly added DLL was not detected.");
            File.Delete(Path.Combine(source.Directory, "plugins", "New.dll"));
            Require(!Scan(source).ContainsDll(source.Id), "A removed DLL retained its badge.");
        });
        test("DLL scan failures and cancellation are explicit", () =>
        {
            using var f = new Program.Fixture(); var source = f.Source("Source", false);
            using var canceled = new CancellationTokenSource(); canceled.Cancel();
            try { Scanner.Scan(new[] { source }, canceled.Token); throw new Exception("Canceled DLL inspection continued."); }
            catch (OperationCanceledException) { }
            try { Scan(source, source); throw new Exception("Duplicate DLL source accepted."); }
            catch (InvalidOperationException) { }
            Directory.Delete(source.Directory);
            try { Scan(source); throw new Exception("A missing disabled directory was reported as containing no DLLs."); }
            catch (DirectoryNotFoundException) { }
        });
        test("DLL inspection failure and recovery do not replace the separate conflict report", () =>
        {
            using var f = new Program.Fixture(); var missing = f.Source("Missing", false); var good = f.Source("Good"); Directory.Delete(missing.Directory);
            int errors = 0;
            using var conflicts = new ScanSession<ConflictReport>(new ConflictScanner().Scan, _ => throw new Exception("Unexpected conflict scan failure."));
            using var dlls = new ScanSession<ModContentInventory>(Scanner.Scan, _ => errors++);
            var sources = new[] { missing, good }; conflicts.Request(sources); dlls.Request(sources);
            ScanOutcome<ConflictReport>? content = null; ScanOutcome<ModContentInventory>? binary = null;
            Require(SpinWait.SpinUntil(() => { content = conflicts.Poll() ?? content; binary = dlls.Poll() ?? binary; return content != null && binary != null; }, 5000), "Independent scans did not complete.");
            Require(content!.Report != null && content.Error == null && binary!.Report == null && binary.Error is DirectoryNotFoundException && errors == 1,
                "A DLL scan failure was hidden or corrupted the independent file-overlap result.");
            Directory.CreateDirectory(missing.Directory); f.Write(missing, "Plugin.dll", "dll"); dlls.Request(sources); binary = null;
            Require(SpinWait.SpinUntil(() => (binary = dlls.Poll()) != null, 5000) && binary!.Error == null && binary.Report!.ContainsDll(missing.Id),
                "A later DLL rescan could not recover.");
            Require(content.Report != null, "DLL recovery discarded an accepted conflict result.");
        });
    }
}
