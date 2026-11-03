using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using Path = System.IO.Path;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Input;
using ModManagerEnhancements;
using ModManagerEnhancements.Core;
using Noesis;
using SeaPower;

internal static class SearchUiTests
{
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }

    internal static void Run(Action<string, Action> test)
    {
        var previous = GUI.GetApplicationResources();
        GUI.SetApplicationResources((ResourceDictionary)GUI.ParseXaml(@"<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
          <SolidColorBrush x:Key='Brush.Glyph.Normal' Color='#D8D8D8'/><SolidColorBrush x:Key='Brush.Button.Background' Color='#303A41'/>
          <Style TargetType='Button'><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Border Background='#404A51'><ContentPresenter Margin='{TemplateBinding Padding}'/></Border></ControlTemplate></Setter.Value></Setter></Style>
          <Style TargetType='TextBox'><Setter Property='MinHeight' Value='24'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='TextBox'><Border Background='{TemplateBinding Background}' Padding='{TemplateBinding Padding}'><ScrollViewer x:Name='PART_ContentHost' HorizontalScrollBarVisibility='Hidden' VerticalScrollBarVisibility='Hidden'/></Border></ControlTemplate></Setter.Value></Setter></Style>
          <Style TargetType='ComboBox'><Setter Property='MinHeight' Value='24'/></Style>
        </ResourceDictionary>"));
        try
        {
            test("native list search combines names IDs wildcards and kinds without changing the source", () =>
            {
                using var f = new Fixture(); var original = f.State.Directories.ToArray();
                foreach (int width in new[] { 480, 800, 1200 })
                {
                    f.Layout(width);
                    Require(f.Search.Header.ActualWidth > 0 && f.Search.KindBox.ActualWidth >= 120,
                        "The new controls do not fit the existing list pane.");
                }
                f.Search.SearchBox.Text = "*F-*"; f.Search.KindBox.SelectedIndex = 3; f.Layout();
                Require(f.Search.Visible.SequenceEqual(new[] { f.F15, f.F2 }) && f.Search.Counter.Text == "2 / 7",
                    "Name/type conditions did not filter the original row objects.");
                Require(f.List.Items.Cast<SearchDirectory>().SequenceEqual(f.Search.Visible), "The native ItemsControl did not receive the filtered projection.");
                f.Search.SearchBox.Text = "FindMeFolder"; f.Layout();
                Require(f.Search.Visible.Count == 0, $"The query did not update correctly: text={f.Search.SearchBox.Text}, visible={string.Join(",", f.Search.Visible.Select(item => item.Name))}.");
                f.Search.SearchBox.Text = "3832319500"; f.Search.KindBox.SelectedIndex = 0; f.Layout();
                Require(f.Search.Visible.Single() == f.Enhancements, "Workshop ID lookup failed.");
                f.Search.KindBox.SelectedIndex = 4; f.Search.SearchBox.Text = "*Enhancements*"; f.Layout();
                f.Click(f.Search.ClearButton); f.Layout();
                Require(f.Search.SearchBox.Text.Length == 0 && f.Search.KindBox.SelectedIndex == 4 && f.Search.Visible.Count == 2,
                    "Clearing the query also cleared the selected kind or failed to update results.");
                f.Search.SetInventory(null, null); f.Layout();
                Require(f.Search.Visible.Count == 0 && ((TextBlock)f.Search.Header.FindName("NoMatchingMods")).Text.Contains("確認中"), "Pending kind inspection used stale classifications.");
                f.Search.SetInventory(null, new IOException("test failure")); f.Layout();
                Require(((TextBlock)f.Search.Header.FindName("NoMatchingMods")).Text.Contains("失敗"), "A failed kind scan looked like a successful empty result.");
                f.Search.SetInventory(f.Inventory, null); f.Layout();
                Require(f.Search.Visible.Count == 2, "Kind inspection could not recover.");
                Require(f.State.Directories.SequenceEqual(original) && f.Mission.IsChecked && !f.OtherMission.IsChecked && f.F15.IsChecked && !f.F2.IsChecked,
                    "Search modified the full source, its order or its enabled states.");
            });
            test("filtered native bulk actions keep captions and affect visible enabled MODs only", () =>
            {
                using var f = new Fixture(); f.Search.SearchBox.Text = "*F-*"; f.Layout();
                var row = f.Rows().First(); var actions = row.ContextMenu.Items.OfType<MenuItem>().ToArray();
                Require((string)actions[0].Header == "全選択" && (string)actions[1].Header == "全解除" &&
                    (string)actions[0].ToolTip == "表示中のMODに限る" && (string)actions[1].ToolTip == "表示中のMODに限る",
                    "The native captions changed or the scoped tooltip was lost.");
                var all = actions[0].Command; var none = actions[1].Command;
                all.Execute(null);
                Require(f.F15.IsChecked && f.F2.IsChecked && !f.OtherMission.IsChecked && f.State.NativeAllCalls == 0,
                    "Filtered select-all reached hidden mods or the original global command.");
                none.Execute(null);
                Require(!f.F15.IsChecked && !f.F2.IsChecked && f.Mission.IsChecked && f.State.NativeNoneCalls == 0,
                    "Filtered select-none reached hidden mods.");
                f.Search.SearchBox.Text = "no matches"; f.Layout(); all.Execute(null);
                Require(!f.OtherMission.IsChecked && !f.F15.IsChecked && !f.F2.IsChecked && f.State.NativeAllCalls == 0,
                    "An empty search fell back to selecting all mods.");
                f.Search.SearchBox.Text = "Original"; f.Layout(); var protectedActions = f.Rows().Single().ContextMenu.Items.OfType<MenuItem>().ToArray();
                protectedActions[1].Command.Execute(null);
                Require(f.Original.IsChecked, "A locked native source was changed by a scoped action.");
                f.Search.SearchBox.Text = ""; f.Layout(); actions = f.Rows().First().ContextMenu.Items.OfType<MenuItem>().ToArray();
                Require(actions.All(item => item.ToolTip == null), "Clearing the filter did not restore native tooltips.");
                actions[0].Command.Execute(null);
                Require(f.State.NativeAllCalls == 1 && f.OtherMission.IsChecked, "Unfiltered bulk selection no longer uses the native action.");
                f.State.AllCaption = "Select all";
                Require((string)actions[0].Header == "Select all", "Scoped commands replaced the native caption binding.");
            });
            test("projection follows native reordering and detach restores bindings and layout", () =>
            {
                using var f = new Fixture(); f.Search.KindBox.SelectedIndex = 3; f.Layout();
                int old = f.State.Directories.IndexOf(f.F2); f.State.Directories.Move(old, f.State.Directories.IndexOf(f.F15));
                f.Search.Refresh(); f.Layout();
                Require(f.Search.Visible.SequenceEqual(new[] { f.F2, f.F15 }), "The projection invented a sort order instead of following the native list.");
                var row = f.Rows().First(); var actions = row.ContextMenu.Items.OfType<MenuItem>().ToArray();
                f.Search.Dispose(); f.Layout();
                Require(f.Host.RowDefinitions.Count == 4 && Grid.GetRow(f.Scroll) == 1 && f.Scroll.Margin.Top == 30 &&
                    !f.Host.Children.Contains(f.Search.Header) && ReferenceEquals(f.List.ItemsSource, f.State.Directories),
                    "Detaching left a search row, shifted native layout or filtered source behind.");
                Require(BindingOperations.GetBindingBase(f.List, ItemsControl.ItemsSourceProperty) != null &&
                    actions.All(item => BindingOperations.GetBindingBase(item, MenuItem.CommandProperty) != null && item.ToolTip == null),
                    "Detaching lost a native source/command binding.");
                f.Search = f.CreateSearch(); f.Layout();
                Require(f.Host.RowDefinitions.Count == 5 && f.Search.Visible.Count == 7,
                    "Reopening retained a filter or duplicated its controls.");
            });
        }
        finally { GUI.SetApplicationResources(previous); }
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly UserControl Root;
        internal readonly Grid Host;
        internal readonly ItemsControl List;
        internal readonly ScrollViewer Scroll;
        internal readonly State State = new();
        internal readonly SearchDirectory Enhancements, Mission, OtherMission, F15, F2, Original;
        internal readonly ModContentInventory Inventory;
        internal ModListSearch Search;
        private readonly View view;
        private double time;
        private readonly string prefix = Path.Combine(Path.GetTempPath(), "MME-search-" + Guid.NewGuid().ToString("N"));
        internal Fixture()
        {
            var anchor = Item("Anchor Chain", "anchor", true, true, 3380210757);
            Enhancements = Item("Mod Manager Enhancements", "enhancements", true, true, 3832319500);
            Mission = Item("Mission 1985", "mission", true); OtherMission = Item("Other Mission", "other", false);
            F15 = Item("F-15J Peace Eagle", "FindMeFolder", true); F2 = Item("F-2A 'Viper Zero'", "f2", false);
            Original = Item("Original", "original", true, false, 0, false);
            foreach (var item in new[] { anchor, Enhancements, Mission, OtherMission, F15, F2, Original }) State.Directories.Add(item);
            Inventory = new ModContentInventory(new Dictionary<string, ModContent>
            {
                [anchor.DirectoryInfo.FullName] = ModContent.Dll, [Enhancements.DirectoryInfo.FullName] = ModContent.Dll,
                [Mission.DirectoryInfo.FullName] = ModContent.Missions, [OtherMission.DirectoryInfo.FullName] = ModContent.Missions,
                [F15.DirectoryInfo.FullName] = ModContent.Units, [F2.DirectoryInfo.FullName] = ModContent.Units
            });
            Root = (UserControl)GUI.ParseXaml(@"<UserControl xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
              <Grid x:Name='NativeHost'><Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='*'/><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/></Grid.RowDefinitions>
                <Button Content='Modding Tools' Margin='30,10,30,5'/>
                <ScrollViewer x:Name='NativeScroll' Grid.Row='1' Margin='30' VerticalScrollBarVisibility='Auto'>
                  <ItemsControl x:Name='NativeList' ItemsSource='{Binding Directories}'><ItemsControl.ItemTemplate><DataTemplate>
                    <Border Padding='0,2,16,2'><Grid><Grid.ContextMenu><ContextMenu>
                      <MenuItem Header='{Binding RelativeSource={RelativeSource AncestorType=UserControl}, Path=DataContext.AllCaption}' Command='{Binding RelativeSource={RelativeSource AncestorType=UserControl}, Path=DataContext.SelectAllCommand}'/>
                      <MenuItem Header='全解除' Command='{Binding RelativeSource={RelativeSource AncestorType=UserControl}, Path=DataContext.SelectNoneCommand}'/>
                    </ContextMenu></Grid.ContextMenu><TextBlock Text='{Binding Name}'/></Grid>
                    </Border>
                  </DataTemplate></ItemsControl.ItemTemplate></ItemsControl>
                </ScrollViewer><Button Grid.Row='2' Content='Sort'/><TextBlock Grid.Row='3'/>
              </Grid></UserControl>");
            Root.DataContext = State; Host = (Grid)Root.FindName("NativeHost"); List = (ItemsControl)Root.FindName("NativeList"); Scroll = (ScrollViewer)Root.FindName("NativeScroll");
            view = GUI.CreateView(Root); view.SetSize(800, 600); view.Update(time += 0.1);
            Search = CreateSearch(); Layout();
        }
        internal ModListSearch CreateSearch()
        {
            var result = new ModListSearch(Host, List, State.Directories, State.SelectAllCommand, State.SelectNoneCommand, new ModText("ja"), () => { });
            result.SetInventory(Inventory, null); return result;
        }
        private SearchDirectory Item(string name, string folder, bool active, bool steam = false, ulong id = 0, bool editable = true)
        {
#pragma warning disable SYSLIB0050
            var item = (SearchDirectory)FormatterServices.GetUninitializedObject(typeof(SearchDirectory));
#pragma warning restore SYSLIB0050
            typeof(SearchDirectory).GetField("_directoryInfo", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(item, new DirectoryInfo(Path.Combine(prefix, folder)));
            typeof(SearchDirectory).GetField("<IsSteam>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(item, steam);
            item.Name = name; item.IsEnabled = editable; item.IsChecked = active; item.steamId = new Steamworks.PublishedFileId_t(id); return item;
        }
        internal Grid[] Rows()
        {
            var rows = new List<Grid>();
            void Visit(DependencyObject node)
            {
                if (node is Grid grid && grid.DataContext is SearchDirectory && grid.ContextMenu != null) rows.Add(grid);
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) Visit(VisualTreeHelper.GetChild(node, i));
            }
            Visit(List); return rows.ToArray();
        }
        internal void Layout(int width = 800)
        {
            view.SetSize(width, 600); view.Update(time += 0.1);
            if (Search != null) Search.UpdateRows(Rows());
        }
        internal void Click(FrameworkElement element)
        {
            var rect = element.TransformToAncestor(Root).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
            int x = (int)(rect.X + rect.Width / 2), y = (int)(rect.Y + rect.Height / 2);
            view.MouseMove(x, y); view.MouseButtonDown(x, y, MouseButton.Left); view.MouseButtonUp(x, y, MouseButton.Left);
        }
        public void Dispose() { Search.Dispose(); view.Dispose(); }
    }

    public sealed class State : INotifyPropertyChanged
    {
        public TrulyObservableCollection<SearchDirectory> Directories { get; } = new();
        public ICommand SelectAllCommand { get; }
        public ICommand SelectNoneCommand { get; }
        public int NativeAllCalls { get; private set; }
        public int NativeNoneCalls { get; private set; }
        private string allCaption = "全選択";
        public string AllCaption { get => allCaption; set { allCaption = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AllCaption))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
        public State()
        {
            SelectAllCommand = new ActionCommand(() => { NativeAllCalls++; foreach (var item in Directories) if (item.IsEnabled) item.IsChecked = true; });
            SelectNoneCommand = new ActionCommand(() => { NativeNoneCalls++; foreach (var item in Directories) if (item.IsEnabled) item.IsChecked = false; });
        }
    }
    private sealed class ActionCommand : ICommand
    {
        private readonly Action execute;
        internal ActionCommand(Action execute) { this.execute = execute; }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => execute();
        public event System.EventHandler? CanExecuteChanged { add { } remove { } }
    }
}
