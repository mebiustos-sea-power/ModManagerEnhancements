using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using ModManagerEnhancements;
using ModManagerEnhancements.Core;
using Noesis;
using SeaPower;
using Path = System.IO.Path;

internal static class CompatibilityUiTests
{
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }

    internal static void Run(Action<string, Action> test)
    {
        test("compatibility menu is first only on eligible rows and follows row reuse and removal", () =>
        {
            using var f = new Fixture();
            f.SetRows(f.First, true); f.SetRows(f.First, true);
            Require(f.Warned.ContextMenu.Items.Count == 3 && f.Healthy.ContextMenu.Items.Count == 2 &&
                ReferenceEquals(f.Warned.ContextMenu.Items[1], f.NativeAll) && ReferenceEquals(f.Warned.ContextMenu.Items[2], f.NativeNone) &&
                f.Warned.ContextMenu.Items.Cast<object>().All(item => item is MenuItem),
                "The added item duplicated, changed native items or added a separator.");
            f.SetRows(f.Second, true); f.Action().Execute(null);
            Require(f.Dialog!.Message.Contains(f.Second.Name) && f.Dialog.Message.Contains(Fixture.Version),
                "The menu retained a previously bound mod.");
            f.Actions.Dispose();
            Require(f.Warned.ContextMenu.Items.Count == 2 && f.Open.Count == 0, "Detaching left a menu item or confirmation behind.");
        });
        test("only explicit confirmation edits or restores the selected mod", () =>
        {
            using var f = new Fixture();
            f.SetRows(f.First, true);
            var change = f.Action(); change.Execute(null);
            var cancelled = f.Dialog!;
            Require(File.ReadAllText(f.FirstFile) == Fixture.Original && f.Reloads == 0 &&
                cancelled.CancelAvailable && cancelled.AcknowledgeAvailable && !cancelled.MiddleAvailable,
                "Opening confirmation changed a file or exposed unexpected actions.");
            Require(cancelled.Message.Contains("動作保証はできません") && cancelled.Message.Contains("対応バージョンを元に戻す") &&
                !cancelled.Message.Contains("元に戻す機能はありません"),
                "Confirmation gave incorrect guarantee or recovery instructions.");
            cancelled.CancelCommand.Execute(null); cancelled.AcknowledgeCommand.Execute(null);
            Require(File.ReadAllText(f.FirstFile) == Fixture.Original && f.Reloads == 0, "Cancel allowed a stale confirmation to write.");
            change.Execute(null); var dismissed = f.Dialog!; f.Open.Remove(dismissed);
            dismissed.AcknowledgeCommand.Execute(null);
            Require(File.ReadAllText(f.FirstFile) == Fixture.Original, "Closing the window allowed confirmation to write.");
            change.Execute(null); var accepted = f.Dialog!; accepted.AcknowledgeCommand.Execute(null); accepted.AcknowledgeCommand.Execute(null);
            Require(CompatibilityEdit.Inspect(f.FirstFile) == CompatibilityOverrideState.Modified &&
                File.ReadAllText(f.SecondFile) == Fixture.Original && f.Reloads == 1 && !f.LastRestore && f.Open.Count == 0,
                "Confirmation failed, ran twice or changed another mod.");

            f.SetRows(f.First, false);
            Require((string)((MenuItem)f.Warned.ContextMenu.Items[0]).Header == "対応バージョンを元に戻す" &&
                f.Warned.ContextMenu.Items.Count == 3, "The changed mod did not gain a restore action.");
            f.Action().Execute(null); var restoreCancelled = f.Dialog!; restoreCancelled.CancelCommand.Execute(null);
            Require(CompatibilityEdit.Inspect(f.FirstFile) == CompatibilityOverrideState.Modified, "Canceling restore changed the file.");
            f.Action().Execute(null); f.Dialog!.AcknowledgeCommand.Execute(null);
            Require(File.ReadAllText(f.FirstFile) == Fixture.Original && f.Reloads == 2 && f.LastRestore &&
                CompatibilityEdit.Inspect(f.FirstFile) == CompatibilityOverrideState.None && f.Open.Count == 0,
                "Restore did not restore the author setting and clear the marker.");
            f.SetRows(f.First, true);
            Require((string)((MenuItem)f.Warned.ContextMenu.Items[0]).Header == "対応バージョンを現在のゲーム版に変更",
                "Returning to an out-of-range author setting did not restore the override action.");
        });
        test("confirmation errors are shown without overwriting a concurrent Workshop update", () =>
        {
            using var f = new Fixture();
            f.SetRows(f.First, true); f.Action().Execute(null);
            File.WriteAllText(f.FirstFile, "[Compatibility]\nApproximateVersion=0.9.3\n");
            var updated = File.ReadAllBytes(f.FirstFile); f.Dialog!.AcknowledgeCommand.Execute(null);
            Require(f.Errors.Count == 1 && f.Dialog!.Kind == "Error" && f.Open.Count == 1 && f.Reloads == 0 &&
                File.ReadAllBytes(f.FirstFile).SequenceEqual(updated), "The error was hidden or author data was overwritten.");
            f.Dialog.AcknowledgeCommand.Execute(null);
            Require(f.Open.Count == 0, "The error dialog could not close.");
        });
        test("a newer game can offer restore first and another override without replacing the original backup", () =>
        {
            using var f = new Fixture();
            CompatibilityEdit.Prepare(f.FirstFile, "0.9.2").Commit();
            f.SetRows(f.First, true);
            Require(f.Warned.ContextMenu.Items.Count == 4 &&
                (string)((MenuItem)f.Warned.ContextMenu.Items[0]).Header == "対応バージョンを元に戻す" &&
                (string)((MenuItem)f.Warned.ContextMenu.Items[1]).Header == "対応バージョンを現在のゲーム版に変更" &&
                ReferenceEquals(f.Warned.ContextMenu.Items[2], f.NativeAll), "Restore and override actions were not ordered correctly.");
            f.Action(1).Execute(null); f.Dialog!.AcknowledgeCommand.Execute(null);
            f.SetRows(f.First, false); f.Action().Execute(null); f.Dialog!.AcknowledgeCommand.Execute(null);
            Require(File.ReadAllText(f.FirstFile) == Fixture.Original, "Another override replaced the first saved author setting.");
        });
        test("restore accepts a manually edited supported version", () =>
        {
            using var f = new Fixture();
            CompatibilityEdit.Prepare(f.FirstFile, Fixture.Version).Commit();
            File.WriteAllText(f.FirstFile, File.ReadAllText(f.FirstFile).Replace("ExactVersion=" + Fixture.Version, "ExactVersion=0.9.4"));
            f.SetRows(f.First, false); f.Action().Execute(null);
            f.Dialog!.AcknowledgeCommand.Execute(null);
            Require(f.Errors.Count == 0 && f.Reloads == 1 && f.LastRestore && File.ReadAllText(f.FirstFile) == Fixture.Original,
                "Restore blocked a manual edit or did not restore the saved setting.");
        });
        test("changed-record inspection follows file updates and reports corrupt metadata once", () =>
        {
            using var f = new Fixture();
            Require(CompatibilityMetadata.GetOverride(f.First, f.Errors.Add) == CompatibilityOverrideState.None, "An unmodified file looked changed.");
            CompatibilityEdit.Prepare(f.FirstFile, Fixture.Version).Commit();
            Require(CompatibilityMetadata.GetOverride(f.First, f.Errors.Add) == CompatibilityOverrideState.Modified, "The marker was not read from disk.");
            File.AppendAllText(f.FirstFile, "\n[ModManagerEnhancements]\nOriginalExactVersionData=broken\n");
            Require(CompatibilityMetadata.GetOverride(f.First, f.Errors.Add) == CompatibilityOverrideState.Unreadable &&
                CompatibilityMetadata.GetOverride(f.First, f.Errors.Add) == CompatibilityOverrideState.Unreadable && f.Errors.Count == 1,
                "Invalid metadata looked unmodified or repeatedly flooded the error log.");
        });
    }

    private sealed class Fixture : IDisposable
    {
        internal const string Version = "0.9.3-261116";
        internal const string Original = "[Language_en]\nName=Example\n[Compatibility]\nExactVersion=0.9.0\n";
        private readonly string root = Path.Combine(Path.GetTempPath(), "mme-compatibility-ui-" + Guid.NewGuid().ToString("N"));
        internal readonly Grid Warned = new() { ContextMenu = new ContextMenu() };
        internal readonly Grid Healthy = new() { ContextMenu = new ContextMenu() };
        internal readonly MenuItem NativeAll = new() { Header = "すべて選択" };
        internal readonly MenuItem NativeNone = new() { Header = "選択解除" };
        internal SearchDirectory First { get; }
        internal SearchDirectory Second { get; }
        internal string FirstFile => Path.Combine(First.DirectoryInfo.FullName, "_info.ini");
        internal string SecondFile => Path.Combine(Second.DirectoryInfo.FullName, "_info.ini");
        internal CompatibilityActions Actions { get; }
        internal HashSet<NativeMessageBox> Open { get; } = new();
        internal List<Exception> Errors { get; } = new();
        internal NativeMessageBox? Dialog { get; private set; }
        internal int Reloads { get; private set; }
        internal bool LastRestore { get; private set; }
        internal Fixture()
        {
            First = Source("First"); Second = Source("Second");
            File.WriteAllText(FirstFile, Original); File.WriteAllText(SecondFile, Original);
            Warned.ContextMenu.Items.Add(NativeAll); Warned.ContextMenu.Items.Add(NativeNone);
            Healthy.ContextMenu.Items.Add(new MenuItem()); Healthy.ContextMenu.Items.Add(new MenuItem());
            Actions = new CompatibilityActions(Version, new ModText("ja"),
                message => { Dialog = message; Open.Add(message); }, message => Open.Remove(message), Open.Contains,
                (_, restore) => { Reloads++; LastRestore = restore; }, Errors.Add);
        }
        internal void SetRows(SearchDirectory source, bool warned) => Actions.UpdateRows(new Dictionary<Grid, CompatibilityRow>
        { [Warned] = new CompatibilityRow(source, warned, CompatibilityEdit.Inspect(Path.Combine(source.DirectoryInfo.FullName, "_info.ini"))) });
        internal System.Windows.Input.ICommand Action(int index = 0) => ((MenuItem)Warned.ContextMenu.Items[index]).Command;
        private SearchDirectory Source(string name)
        {
            string folder = Path.Combine(root, name); Directory.CreateDirectory(folder);
#pragma warning disable SYSLIB0050
            var source = (SearchDirectory)FormatterServices.GetUninitializedObject(typeof(SearchDirectory));
#pragma warning restore SYSLIB0050
            typeof(SearchDirectory).GetField("_directoryInfo", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(source, new DirectoryInfo(folder));
            source.Name = name;
            return source;
        }
        public void Dispose() { Actions.Dispose(); Directory.Delete(root, true); }
    }
}
