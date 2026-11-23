using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Input;
using ModManagerEnhancements;
using ModManagerEnhancements.Core;
using Noesis;
using SeapowerUI.ViewModels;
using EventArgs = System.EventArgs;
using EventHandler = System.EventHandler;

internal static class SyncUiTests
{
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }

    internal static void Run(Action<string, Action> test)
    {
        test("sync button and dependency menu require explicit consent and execute once", () =>
        {
            using var f = new Fixture("ja");
            var parameter = new object();
            f.Button.Command.Execute(parameter);
            var cancelled = f.Dialog!;
            Require(f.Native.Executions == 0 && f.Open.Count == 1, "Opening confirmation started synchronization.");
            cancelled.CancelCommand.Execute(null); cancelled.AcknowledgeCommand.Execute(null);
            Require(f.Native.Executions == 0 && f.Open.Count == 0, "Cancellation or an old confirmation started synchronization.");
            f.Button.Command.Execute(parameter);
            var accepted = f.Dialog!;
            f.Menu.Command.Execute(new object());
            Require(f.Shown == 2 && f.Open.Count == 1, "The two native entry points created duplicate confirmations.");
            accepted.AcknowledgeCommand.Execute(null); accepted.AcknowledgeCommand.Execute(null);
            Require(f.Native.Executions == 1 && ReferenceEquals(f.Native.Parameter, parameter) && f.Open.Count == 0,
                "Consent repeated synchronization or lost its native command parameter.");
            f.Menu.Command.Execute(null); f.Dialog!.AcknowledgeCommand.Execute(null);
            Require(f.Native.Executions == 2 && f.Errors.Count == 0, "The dependency menu bypassed confirmation or failed to synchronize.");
        });
        test("sync confirmation preserves native availability and rechecks it after consent", () =>
        {
            using var f = new Fixture("en");
            int changes = 0;
            f.Confirmation.CanExecuteChanged += (_, _) => changes++;
            f.Native.Enabled = false; f.Native.Changed();
            f.Confirmation.Execute(null);
            Require(!f.Confirmation.CanExecute(null) && f.Shown == 0 && changes == 1,
                "The wrapper ignored native command availability or its change event.");
            f.Native.Enabled = true; f.Native.Changed();
            f.Confirmation.Execute(null);
            f.Native.Enabled = false;
            f.Dialog!.AcknowledgeCommand.Execute(null);
            Require(f.Native.Executions == 0 && f.Open.Count == 0,
                "A native command that became unavailable executed after consent.");
            f.Confirmation.Dispose();
            int before = changes;
            f.Native.Changed();
            Require(f.Native.Subscribers == 0 && changes == before && !f.Confirmation.CanExecute(null),
                "Disposal retained native command subscriptions or an executable stale wrapper.");
        });
        test("dismissed sync dialogs cannot act on a newer confirmation", () =>
        {
            using var f = new Fixture("en");
            f.Confirmation.Execute(null);
            var old = f.Dialog!;
            f.Open.Remove(old);
            old.AcknowledgeCommand.Execute(null);
            Require(f.Native.Executions == 0, "An externally closed dialog still executed synchronization.");
            f.Confirmation.Execute(null);
            var current = f.Dialog!;
            old.CancelCommand.Execute(null); old.AcknowledgeCommand.Execute(null);
            Require(f.Open.Contains(current), "An old dialog closed the current confirmation.");
            current.AcknowledgeCommand.Execute(null);
            Require(f.Native.Executions == 1 && f.Errors.Count == 0, "Replacing a dismissed dialog lost or repeated synchronization.");
        });
        test("unloaded sync screens cannot execute and a recreated language context gets fresh commands", () =>
        {
            using var old = new Fixture("ja");
            old.Confirmation.Execute(null);
            var stale = old.Dialog!;
            Require(stale.AcknowledgeCommandLabel == "同期", "The initial confirmation did not use the active language.");
            old.Dispose();
            using var fresh = new Fixture("en");
            Require(!ReferenceEquals(old.State.LanguageResourceHandler, fresh.State.LanguageResourceHandler), "The test did not replace the language object.");
            stale.AcknowledgeCommand.Execute(null); old.Confirmation.Execute(null);
            fresh.Button.Command.Execute(null);
            Require(old.Open.Count == 0 && old.Native.Executions == 0 && old.Native.Subscribers == 0 &&
                fresh.Dialog!.AcknowledgeCommandLabel == "Sync" && fresh.Open.Count == 1,
                "The new screen retained old language/command state or the disposed screen remained active.");
            fresh.Dialog!.AcknowledgeCommand.Execute(null);
            Require(fresh.Native.Executions == 1 && old.Errors.Count == 0 && fresh.Errors.Count == 0,
                "The recreated screen failed or repeated synchronization.");
        });
        test("unloading while the sync confirmation closes does not execute the old command", () =>
        {
            using var f = new Fixture("en");
            f.AfterClose = f.Confirmation.Dispose;
            f.Confirmation.Execute(null); f.Dialog!.AcknowledgeCommand.Execute(null);
            Require(f.Native.Executions == 0 && f.Native.Subscribers == 0 && f.Open.Count == 0 && f.Errors.Count == 0,
                "Closing the confirmation executed a command whose screen was disposed during that close.");
        });
        test("sync dialog failures are logged and never start synchronization without a completed confirmation", () =>
        {
            using var f = new Fixture("en");
            f.FailShow = true; f.Confirmation.Execute(null);
            Require(f.Native.Executions == 0 && f.Errors.Count == 1, "A dialog-open failure silently started synchronization.");
            f.FailShow = false; f.Confirmation.Execute(null);
            f.FailClose = true; f.Dialog!.AcknowledgeCommand.Execute(null);
            Require(f.Native.Executions == 0 && f.Errors.Count == 2, "A dialog-close failure started synchronization or was hidden.");
            f.FailClose = false; f.Open.Clear(); f.Confirmation.Execute(null);
            f.Dialog!.CancelCommand.Execute(null);
            Require(f.Open.Count == 0 && f.Native.Executions == 0, "Confirmation did not recover after a dialog failure.");
        });
        test("native sync command replacement notifies bindings and restores cleanly across repeated screen visits", () =>
        {
            var native = new Command();
#pragma warning disable SYSLIB0050
            var model = (ModMenuViewModel)FormatterServices.GetUninitializedObject(typeof(ModMenuViewModel));
#pragma warning restore SYSLIB0050
            // Only managed command/property-change members are used; no native Noesis object was constructed.
            GC.SuppressFinalize(model);
            var field = typeof(ModMenuViewModel).GetField("<SyncDependenciesCommand>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
            field.SetValue(model, native);
            int notifications = 0;
            model.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(ModMenuViewModel.SyncDependenciesCommand)) notifications++; };
            for (int visit = 0; visit < 3; visit++)
            {
                using var binding = new NativeSyncCommand(model);
                using var confirmation = new SyncConfirmation(binding.Original, TestText.Create(visit == 0 ? "ja" : "en"),
                    _ => { }, _ => { }, _ => false, error => throw error);
                binding.Replace(confirmation);
                Require(ReferenceEquals(model.SyncDependenciesCommand, confirmation) && native.Subscribers == 1,
                    "The native getter bypassed confirmation or duplicate event subscriptions remained.");
                confirmation.Dispose(); binding.Dispose();
                Require(ReferenceEquals(model.SyncDependenciesCommand, native) && native.Subscribers == 0,
                    "Leaving the screen did not restore its native command or detach its event.");
            }
            Require(notifications == 6, "Installing/restoring the command did not refresh both native binding consumers.");
            using var replaced = new NativeSyncCommand(model);
            var other = new Command();
            replaced.Replace(new Command()); field.SetValue(model, other); replaced.Dispose();
            Require(ReferenceEquals(model.SyncDependenciesCommand, other), "Cleanup overwrote a later command replacement.");
        });
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly Command Native = new();
        internal readonly State State;
        internal readonly SyncConfirmation Confirmation;
        internal readonly Button Button;
        internal readonly MenuItem Menu;
        internal readonly HashSet<NativeMessageBox> Open = new();
        internal readonly List<Exception> Errors = new();
        internal NativeMessageBox? Dialog;
        internal int Shown;
        internal bool FailShow, FailClose;
        internal Action? AfterClose;
        private readonly UserControl root;
        private readonly View view;
        private bool disposed;

        internal Fixture(string language)
        {
            State = new State(language);
            Confirmation = new SyncConfirmation(Native, TestText.Create(State.LanguageResourceHandler.Language), message =>
            {
                Dialog = message; Shown++;
                if (FailShow) throw new InvalidOperationException("Open failed.");
                Open.Add(message);
            }, message =>
            {
                if (FailClose) throw new InvalidOperationException("Close failed.");
                Open.Remove(message);
                AfterClose?.Invoke();
            }, Open.Contains, Errors.Add);
            State.SyncDependenciesCommand = Confirmation;
            root = (UserControl)GUI.ParseXaml(@"<UserControl xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
              <StackPanel><Button x:Name='Sync' Content='Sync' Command='{Binding SyncDependenciesCommand}'/>
                <Grid x:Name='Row' Width='300' Height='30'><Grid.ContextMenu><ContextMenu>
                  <MenuItem Header='Download dependencies' Command='{Binding RelativeSource={RelativeSource AncestorType=UserControl}, Path=DataContext.SyncDependenciesCommand}'/>
                </ContextMenu></Grid.ContextMenu></Grid>
              </StackPanel></UserControl>");
            root.DataContext = State;
            root.Unloaded += Unloaded;
            Button = (Button)root.FindName("Sync");
            var row = (Grid)root.FindName("Row"); Menu = (MenuItem)row.ContextMenu.Items[0];
            view = GUI.CreateView(root); view.SetSize(800, 600); view.Update(0);
            row.ContextMenu.IsOpen = true; view.Update(0.1);
            Require(ReferenceEquals(Button.Command, Confirmation) && ReferenceEquals(Menu.Command, Confirmation),
                "The native button/context-menu binding shapes did not share the confirmation command.");
        }
        private void Unloaded(object sender, RoutedEventArgs args) => Confirmation.Dispose();
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            view.Dispose(); root.Unloaded -= Unloaded; Confirmation.Dispose();
        }
    }

    public sealed class State
    {
        public LanguageContext LanguageResourceHandler { get; }
        public ICommand SyncDependenciesCommand { get; set; } = null!;
        internal State(string language) { LanguageResourceHandler = new LanguageContext(language); }
    }
    public sealed class LanguageContext
    {
        public string Language { get; }
        internal LanguageContext(string language) { Language = language; }
    }
    private sealed class Command : ICommand
    {
        internal bool Enabled = true;
        internal int Executions;
        internal object? Parameter;
        private EventHandler? changed;
        internal int Subscribers => changed?.GetInvocationList().Length ?? 0;
        public bool CanExecute(object? parameter) => Enabled;
        public void Execute(object? parameter) { Executions++; Parameter = parameter; }
        public event EventHandler? CanExecuteChanged { add { changed += value; } remove { changed -= value; } }
        internal void Changed() => changed?.Invoke(this, EventArgs.Empty);
    }
}
