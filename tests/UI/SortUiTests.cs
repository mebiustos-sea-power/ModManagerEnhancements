using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Windows.Input;
using ModManagerEnhancements;
using Noesis;
using SeaPower;
using EventHandler = System.EventHandler;

internal static class SortUiTests
{
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    internal static void Run(Action<string, Action> test)
    {
        test("the entire sort button only opens a dismissible menu and keeps its localized caption", () =>
        {
            using var f = new Fixture(); var before = f.State.Directories.ToArray();
            var caption = BindingOperations.GetBindingBase(f.Button, ContentControl.ContentProperty);
            f.Attach();
            foreach (string language in new[] { "Sort", "並べ替え" })
            foreach (int width in new[] { 480, 800 })
            {
                f.State.Caption = language; f.Layout(width);
                var label = Elements<TextBlock>(f.Button).Single(); var arrow = Elements<Noesis.Path>(f.Button).Single();
                Require(label.Text == language && arrow.ActualWidth > 0 && arrow.ActualHeight > 0 &&
                    ReferenceEquals(caption, BindingOperations.GetBindingBase(f.Button, ContentControl.ContentProperty)),
                    "The dropdown lost the native caption binding or its arrow.");
                foreach (float part in new[] { 0.1f, 0.5f, 0.9f })
                {
                    f.ClickButton(part); f.Layout(width);
                    Require(f.Menu.IsOpen && f.Native.Executions == 0 && f.State.Directories.SequenceEqual(before),
                        "Clicking part of the sort button sorted immediately or failed to open the menu.");
                    f.View.KeyDown(Key.Escape); f.View.KeyUp(Key.Escape); f.Layout(width);
                    Require(!f.Menu.IsOpen && f.Native.Executions == 0 && f.State.Directories.SequenceEqual(before),
                        "Dismissing the menu executed an action or failed to close it.");
                }
            }
            f.ClickButton(); f.Layout(); f.OutsideClick(); f.Layout();
            Require(!f.Menu.IsOpen && f.Native.Executions == 0, "Clicking outside did not dismiss the action menu without sorting.");
            f.ClickButton(); f.Layout();
            var parameter = new object(); f.Actions[0].Command.Execute(parameter); f.Layout();
            Require(f.Native.Executions == 1 && ReferenceEquals(parameter, f.Native.Parameter) && !f.Menu.IsOpen,
                "Selecting dependency sorting lost the native command, parameter, or menu dismissal.");
        });
        test("grouping works without native sorting availability and uses the current full collection", () =>
        {
            using var f = new Fixture(); f.Attach();
            var old = f.State.Directories.ToArray();
            var current = new ObservableCollection<SearchDirectory>(Enumerable.Reverse(old));
            current[1].IsChecked = true; current[2].IsChecked = false;
            f.State.Directories = current;
            var expected = current.Where(item => item.Kind != LayerKind.Base && item.IsChecked)
                .Concat(current.Where(item => item.Kind != LayerKind.Base && !item.IsChecked)).Concat(new[] { f.BaseGame }).ToArray();
            var flags = current.Select(item => item.IsChecked).ToArray(); var source = current.ToArray();
            f.Native.Available = false; f.Native.Notify(); f.Layout();
            f.ClickButton(); f.Layout();
            Require(f.Button.IsEnabled && !f.Actions[0].IsEnabled && f.Actions[1].IsEnabled,
                "Native dependency-sort availability incorrectly disabled grouping or the dropdown.");
            f.Actions[0].Command.Execute(null);
            Require(f.Native.Executions == 0, "An unavailable native dependency sort executed.");
            f.Actions[1].Command.Execute(null); f.Layout();
            Require(current.SequenceEqual(expected) && !f.Menu.IsOpen && f.Native.Executions == 0 &&
                source.Select(item => item.IsChecked).SequenceEqual(flags),
                "Grouping used an obsolete collection, changed flags, or invoked dependency sorting.");
            int moves = 0; current.CollectionChanged += (_, __) => moves++;
            f.Actions[1].Command.Execute(null);
            Require(moves == 0 && current.SequenceEqual(expected), "Grouping an already grouped list needlessly rebuilt its rows.");
        });
        test("base game remains last without explicit moves regardless of its name selection or starting position", () =>
        {
            using var f = new Fixture(); f.Attach();
            var mods = f.State.Directories.Where(item => item.Kind != LayerKind.Base).ToArray();
            mods[0].Name = "Base Game"; f.BaseGame.Name = "ゲーム本体";
            foreach (bool enabled in new[] { true, false })
            for (int position = 0; position <= mods.Length; position++)
            {
                f.BaseGame.IsChecked = enabled;
                var current = new ObservableCollection<SearchDirectory>(mods); current.Insert(position, f.BaseGame);
                f.State.Directories = current;
                var before = current.ToArray(); var flags = before.Select(item => item.IsChecked).ToArray();
                bool movedBase = false;
                current.CollectionChanged += (_, args) =>
                {
                    if (args.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Move &&
                        args.NewItems!.Cast<SearchDirectory>().Contains(f.BaseGame)) movedBase = true;
                };
                f.Actions[1].Command.Execute(null);
                Require(ReferenceEquals(current.Last(), f.BaseGame) && !movedBase,
                    "Grouping moved Base Game as a normal mod or did not keep it last.");
                Require(current.Where(item => item.Kind != LayerKind.Base && item.IsChecked).SequenceEqual(mods.Where(item => item.IsChecked)) &&
                    current.Where(item => item.Kind != LayerKind.Base && !item.IsChecked).SequenceEqual(mods.Where(item => !item.IsChecked)) &&
                    before.Select(item => item.IsChecked).SequenceEqual(flags) && f.Native.Executions == 0,
                    "Pinning Base Game changed the normal mod order or enabled states, or invoked native sorting.");
            }
        });
        test("sort teardown restores native bindings and blocks stale actions across repeated visits", () =>
        {
            using var f = new Fixture();
            var originalCommand = BindingOperations.GetBindingBase(f.Button, Button.CommandProperty);
            var originalContext = f.Button.ContextMenu;
            int subscribers = f.Native.Subscribers;
            for (int visit = 0; visit < 3; visit++)
            {
                f.Attach(); f.ClickButton(); f.Layout();
                var menu = f.Menu; var native = f.Actions[0].Command; var group = f.Actions[1].Command; var open = f.Button.Command;
                f.Sort!.Dispose(); f.Layout();
                var before = f.State.Directories.ToArray();
                Require(!menu.IsOpen && f.Button.ContentTemplate == null && ReferenceEquals(f.Button.ContextMenu, originalContext) &&
                    ReferenceEquals(f.Button.Command, f.Native) &&
                    ReferenceEquals(BindingOperations.GetBindingBase(f.Button, Button.CommandProperty), originalCommand) &&
                    f.Native.Subscribers == subscribers,
                    $"Detaching retained native state: open={menu.IsOpen}, template={f.Button.ContentTemplate == null}, context={ReferenceEquals(f.Button.ContextMenu, originalContext)}, command={ReferenceEquals(f.Button.Command, f.Native)}, binding={ReferenceEquals(BindingOperations.GetBindingBase(f.Button, Button.CommandProperty), originalCommand)}, subscribers={f.Native.Subscribers}/{subscribers}, visit={visit}.");
                native.Execute(null); group.Execute(null); open.Execute(null);
                Require(!native.CanExecute(null) && !group.CanExecute(null) && !menu.IsOpen &&
                    f.Native.Executions == 0 && f.State.Directories.SequenceEqual(before), "A stale menu changed the next screen's state.");
            }
            f.ClickButton(); f.Layout();
            Require(f.Native.Executions == 1 && f.Errors.Count == 0, "Restoration duplicated or lost the original native action.");
        });
    }

    private static IEnumerable<T> Elements<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T match) yield return match;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in Elements<T>(VisualTreeHelper.GetChild(root, i))) yield return child;
    }

    private sealed class Fixture : IDisposable
    {
        internal State State { get; } = new();
        internal readonly Command Native = new();
        internal readonly UserControl Root;
        internal readonly Button Button;
        internal readonly View View;
        internal readonly List<Exception> Errors = new();
        internal readonly SearchDirectory BaseGame;
        internal ModSortMenu? Sort;
        internal ContextMenu Menu => Button.ContextMenu;
        internal MenuItem[] Actions => Menu.Items.OfType<MenuItem>().ToArray();
        private double time;
        internal Fixture()
        {
            State.SortByDependenciesCommand = Native;
            Root = (UserControl)GUI.ParseXaml(@"<UserControl xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
              <UserControl.Resources><Style TargetType='Button'><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'>
                <Border Background='#404A51'><ContentPresenter Content='{TemplateBinding Content}' ContentTemplate='{TemplateBinding ContentTemplate}' HorizontalAlignment='Center' VerticalAlignment='Center'/></Border>
              </ControlTemplate></Setter.Value></Setter></Style></UserControl.Resources>
              <Grid Background='#202830'><Button x:Name='Sort' Width='250' Height='36' VerticalAlignment='Bottom' Margin='0,0,0,40'
                Content='{Binding Caption}' Command='{Binding SortByDependenciesCommand}'><Button.ContextMenu><ContextMenu><MenuItem Header='Original'/></ContextMenu></Button.ContextMenu></Button></Grid></UserControl>");
            Root.DataContext = State; Button = (Button)Root.FindName("Sort");
            foreach (bool enabled in new[] { false, true, false, true, true, false })
            {
#pragma warning disable SYSLIB0050
                var item = (SearchDirectory)FormatterServices.GetUninitializedObject(typeof(SearchDirectory));
#pragma warning restore SYSLIB0050
                typeof(SearchDirectory).GetField("<Kind>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(item, LayerKind.Mod);
                item.IsEnabled = true; item.IsChecked = enabled; State.Directories.Add(item);
            }
#pragma warning disable SYSLIB0050
            BaseGame = (SearchDirectory)FormatterServices.GetUninitializedObject(typeof(SearchDirectory));
#pragma warning restore SYSLIB0050
            typeof(SearchDirectory).GetField("<Kind>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(BaseGame, LayerKind.Base);
            BaseGame.IsChecked = true; State.Directories.Add(BaseGame);
            View = GUI.CreateView(Root); Layout();
        }
        internal void Attach() { Sort = new ModSortMenu(Button, Native, () => State.Directories, TestText.Create("ja"), Errors.Add); Layout(); }
        internal void Layout(int width = 800) { View.SetSize(width, 400); View.Update(time += 0.1); }
        internal void ClickButton(float part = 0.5f)
        {
            var rect = Button.TransformToAncestor(Root).TransformBounds(new Rect(0, 0, Button.ActualWidth, Button.ActualHeight));
            Click((int)(rect.X + rect.Width * part), (int)(rect.Y + rect.Height / 2));
        }
        private void Click(int x, int y) { View.MouseMove(x, y); View.MouseButtonDown(x, y, MouseButton.Left); View.MouseButtonUp(x, y, MouseButton.Left); }
        internal void OutsideClick() => Click(5, 5);
        public void Dispose() { Sort?.Dispose(); View.Dispose(); }
    }
    public sealed class State : INotifyPropertyChanged
    {
        private string caption = "Sort";
        public string Caption { get => caption; set { caption = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Caption))); } }
        public ObservableCollection<SearchDirectory> Directories { get; set; } = new();
        public ICommand SortByDependenciesCommand { get; set; } = null!;
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    private sealed class Command : ICommand
    {
        internal bool Available = true;
        internal int Executions;
        internal object? Parameter;
        private EventHandler? changed;
        internal int Subscribers => changed?.GetInvocationList().Length ?? 0;
        public bool CanExecute(object? parameter) => Available;
        public void Execute(object? parameter) { Executions++; Parameter = parameter; }
        public event EventHandler? CanExecuteChanged { add { changed += value; } remove { changed -= value; } }
        internal void Notify() => changed?.Invoke(this, System.EventArgs.Empty);
    }
}
