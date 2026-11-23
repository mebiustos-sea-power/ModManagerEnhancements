using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Input;
using EventHandler = System.EventHandler;
using ModManagerEnhancements;
using ModManagerEnhancements.Core;
using Noesis;
using SeaPower;
using SeapowerUI.ViewModels;

internal static class ControllerUiTests
{
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private static FieldInfo Field(string name) => typeof(ManagerController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static MethodInfo Method(string name) => typeof(ManagerController).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!;

    internal static void Run(Action<string, Action> test)
    {
        test("a malformed translation still tears down the manager and invalidates pending actions", () =>
        {
#pragma warning disable SYSLIB0050
            var controller = (ManagerController)FormatterServices.GetUninitializedObject(typeof(ManagerController));
            var model = (ModMenuViewModel)FormatterServices.GetUninitializedObject(typeof(ModMenuViewModel));
#pragma warning restore SYSLIB0050
            // Construct only the managed teardown state; mounting a game view would require a live Unity scene.
            GC.SuppressFinalize(model);
            var observed = new List<INotifyPropertyChanged>();
            var filter = new ModFilter(); filter.Toggle("/example", "Example");
            var text = new ModText(key => key == "FilteredSelection" ? "{1}" : key);
            Field("rows").SetValue(controller, new Dictionary<Grid, RowBadge>());
            Field("observed").SetValue(controller, observed);
            Field("filter").SetValue(controller, filter);
            Field("text").SetValue(controller, text);
            Field("details").SetValue(controller, new Border());
            Field("mounted").SetValue(controller, true);
            var directories = new TrulyObservableCollection<SearchDirectory>();
            Method("Observe").Invoke(controller, new object[] { directories });
            var item = new ObservedItem();
            item.PropertyChanged += (PropertyChangedEventHandler)Delegate.CreateDelegate(typeof(PropertyChangedEventHandler), controller, Method("ItemChanged"));
            observed.Add(item);

            var command = new Command();
            typeof(ModMenuViewModel).GetField("<SyncDependenciesCommand>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(model, command);
            using var replacement = new NativeSyncCommand(model);
            var open = new HashSet<NativeMessageBox>(); NativeMessageBox? dialog = null;
            using var confirmation = new SyncConfirmation(command, text, message => { dialog = message; open.Add(message); },
                message => open.Remove(message), open.Contains, Plugin.Error);
            replacement.Replace(confirmation);
            Field("syncCommand").SetValue(controller, replacement);
            Field("syncConfirmation").SetValue(controller, confirmation);
            using var scan = new ScanSession<ConflictReport>((_, __) => throw new InvalidOperationException("No scan should run in this test."), Plugin.Error);
            using var content = new ScanSession<ModContentInventory>((_, __) => throw new InvalidOperationException("No scan should run in this test."), Plugin.Error);
            Field("session").SetValue(controller, scan);
            Field("contentSession").SetValue(controller, content);
            Plugin.Errors.Clear(); Plugin.Active = controller;
            try
            {
                confirmation.Execute(null);
                Require(dialog != null && open.Count == 1 && item.Subscribers == 1 && command.Subscribers == 1,
                    "The fixture must begin with a pending confirmation and live subscriptions.");
                Exception? failure = null;
                try { Method("Render").Invoke(controller, null); }
                catch (TargetInvocationException error) when (error.InnerException is FormatException) { failure = error.InnerException; }
                Require(failure is FormatException, "The malformed selected-mod translation did not reproduce the rendering error.");

                controller.Fail(failure!);
                Require(Plugin.Errors.Count == 1 && ReferenceEquals(Plugin.Errors[0], failure), "The original translation error must remain visible in the log.");
                Require(Plugin.Active == null && item.Subscribers == 0 && command.Subscribers == 0,
                    "Error teardown retained the active controller or event subscriptions.");
                Require(ReferenceEquals(model.SyncDependenciesCommand, command) && open.Count == 0,
                    "Error teardown did not restore the native command and close its pending confirmation.");
                dialog!.AcknowledgeCommand.Execute(null);
                Require(command.Executions == 0, "Acknowledging a stale dialog executed synchronization after teardown.");
                Require(Disposed(() => scan.Request(Array.Empty<ModSource>())) && Disposed(() => content.Request(Array.Empty<ModSource>())),
                    "Error teardown left a scan session active.");
                directories.Clear();
            }
            finally
            {
                Method("Unmount").Invoke(controller, null);
                Plugin.Active = null; Plugin.Errors.Clear();
            }
        });
    }

    private static bool Disposed(Action action)
    {
        try { action(); return false; }
        catch (ObjectDisposedException) { return true; }
    }
    private sealed class ObservedItem : INotifyPropertyChanged
    {
        private PropertyChangedEventHandler? changed;
        internal int Subscribers => changed?.GetInvocationList().Length ?? 0;
        public event PropertyChangedEventHandler? PropertyChanged { add { changed += value; } remove { changed -= value; } }
    }
    private sealed class Command : ICommand
    {
        private EventHandler? changed;
        internal int Subscribers => changed?.GetInvocationList().Length ?? 0;
        internal int Executions;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) { Executions++; }
        public event EventHandler? CanExecuteChanged { add { changed += value; } remove { changed -= value; } }
    }
}
