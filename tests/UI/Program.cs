using System;
using System.Collections.Generic;
using System.IO;
using Path = System.IO.Path;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using ModManagerEnhancements;
using ModManagerEnhancements.Core;
using Noesis;
using SeaPower;

internal static class Program
{
    private static int passed;
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Test(string name, Action action) { action(); passed++; Console.WriteLine("PASS " + name); }
    private static bool Same(Brush left, Brush right)
    {
        var a = ((SolidColorBrush)left).Color; var b = ((SolidColorBrush)right).Color;
        return a.A == b.A && a.R == b.R && a.G == b.G && a.B == b.B;
    }

    private static int Main()
    {
        try
        {
            if (Type.GetType("Mono.Runtime") == null) throw new PlatformNotSupportedException("Use the standalone game Mono runner.");
            GUI.Init();
            try
            {
                Test("DLL and A precede overrides while source captions retain the native right edge", () =>
                {
                    using var f = new Fixture();
                    foreach (int width in new[] { 480, 800, 1200 })
                        foreach (bool nativeA in new[] { false, true })
                        foreach (bool workshopSource in new[] { false, true })
                        foreach (var report in new[] { new ConflictReport(new[] { f.Report.Files[0] }), f.Report, new ConflictReport(new[] { f.Report.Files[1] }) })
                        {
                            f.Anchor.Visibility = nativeA ? Visibility.Visible : Visibility.Collapsed;
                            f.Workshop.Visibility = workshopSource ? Visibility.Visible : Visibility.Collapsed;
                            f.Local.Visibility = workshopSource ? Visibility.Collapsed : Visibility.Visible;
                            f.Badge.Update(new BadgeModel(report, f.Id, null), true, new ModText("en"));
                            f.Layout(width);
                            var dll = f.Bounds(f.Dll); var arrows = f.Bounds(f.Arrows);
                            var selection = (Border)f.Arrows.Content;
                            var label = (TextBlock)selection.Child;
                            var naturalLabel = new TextBlock {
                                Text = string.Concat(label.Inlines.Cast<Run>().Select(run => run.Text)),
                                FontFamily = label.FontFamily, FontSize = label.FontSize,
                                FontWeight = label.FontWeight, FontStyle = label.FontStyle, FontStretch = label.FontStretch };
                            naturalLabel.Measure(new Size(float.PositiveInfinity, float.PositiveInfinity));
                            float naturalWidth = naturalLabel.DesiredSize.Width + f.Arrows.Padding.Left + f.Arrows.Padding.Right
                                + selection.Padding.Left + selection.Padding.Right + selection.BorderThickness.Left + selection.BorderThickness.Right;
                            Require(Math.Abs(arrows.Width - naturalWidth) < 0.5f,
                                $"The override button reserves invisible width beyond its text and padding: actual={arrows.Width}, natural={naturalWidth}.");
                            FrameworkElement sourceCaption = workshopSource ? (FrameworkElement)f.Workshop : f.Local;
                            var caption = f.Bounds(sourceCaption);
                            Require(dll.Width > 0 && arrows.Width > 0 && dll.X + dll.Width <= arrows.X && arrows.X + arrows.Width <= caption.X,
                                "The override action must stay between the status badges and the original source caption.");
                            Require(Math.Abs(caption.X + caption.Width - (f.Row.ActualWidth - sourceCaption.Margin.Right)) < 0.25f,
                                "The Workshop/Local caption moved away from its native right edge.");
                            if (nativeA)
                            {
                                var anchor = f.Bounds(f.Anchor);
                                Require(dll.X + dll.Width <= anchor.X && anchor.X + anchor.Width <= arrows.X,
                                    "The expected DLL, A, overrides order was lost.");
                            }
                            f.Badge.Update(new BadgeModel(null, f.Id, null), true, new ModText("en")); f.Layout(width);
                            var withoutArrows = f.Bounds(sourceCaption);
                            Require(Math.Abs(withoutArrows.X + withoutArrows.Width - caption.X - caption.Width) < 0.25f,
                                "Showing or hiding an override badge shifted the source caption's right edge.");
                        }
                    f.Source.IsChecked = false;
                    f.Badge.Update(new BadgeModel(null, f.Id, null), true, new ModText("ja")); f.Layout(800);
                    Require(f.Dll.Visibility == Visibility.Visible && f.Arrows.Visibility == Visibility.Collapsed && f.Bounds(f.Dll).Width > 0,
                        "A disabled or conflict-free DLL mod must retain its independent badge.");
                    f.Badge.Update(new BadgeModel(f.Report, f.Id, null), false, new ModText("en")); f.Layout(800);
                    Require(f.Dll.Visibility == Visibility.Collapsed && f.Arrows.Visibility == Visibility.Visible,
                        "Data-only mods must retain override controls without an incorrect DLL badge.");
                });
                Test("only the override button filters and existing native actions remain usable", () =>
                {
                    using var f = new Fixture();
                    f.Badge.Update(new BadgeModel(f.Report, f.Id, null), true, new ModText("en")); f.Layout(800);
                    var dll = (TextBlock)f.Dll.Child;
                    var dllColor = ((SolidColorBrush)dll.Foreground).Color;
                    var nameColor = ((SolidColorBrush)f.NameLabel.Foreground).Color;
                    Require(f.Dll.BorderThickness.Left + f.Dll.BorderThickness.Top + f.Dll.BorderThickness.Right + f.Dll.BorderThickness.Bottom == 0
                        && dll.FontSize < f.NameLabel.FontSize && Math.Max(dllColor.R, Math.Max(dllColor.G, dllColor.B)) < Math.Min(nameColor.R, Math.Min(nameColor.G, nameColor.B)),
                        "DLL information must be borderless, smaller and visually quieter than the mod name.");
                    var selection = (Border)f.Arrows.Content;
                    var label = (TextBlock)selection.Child;
                    var runs = label.Inlines.Cast<Run>().ToArray();
                    Brush normalUp = runs[0].Foreground, normalDown = runs[2].Foreground;
                    Brush dllText = ((TextBlock)f.Dll.Child).Foreground;
                    f.Click(f.Dll); Require(f.Filters == 0, "The informational DLL badge triggered a filter.");
                    f.Click(f.Workshop); Require(f.NativeClicks == 1 && f.Filters == 0, "Moving the arrows broke the native Workshop action.");
                    f.Click(f.Check); Require(f.Check.IsChecked == true && f.Filters == 0, "Moving the badges broke the native enable checkbox.");
                    f.Click(f.Arrows); f.Layout(800);
                    Require(f.Filters == 1 && f.Filter.Id == f.Id, "The override control no longer selects its source mod.");
                    Require(!Same(runs[0].Foreground, normalUp) && !Same(runs[2].Foreground, normalDown) &&
                        ((SolidColorBrush)selection.BorderBrush).Color.A > 0 && Same(((TextBlock)f.Dll.Child).Foreground, dllText),
                        "Selection highlighting was lost or leaked into the neutral DLL badge.");
                    f.Click(f.Arrows); f.Layout(800);
                    Require(f.Filters == 2 && f.Filter.Id == null && Same(runs[0].Foreground, normalUp) && Same(runs[2].Foreground, normalDown)
                        && ((SolidColorBrush)selection.BorderBrush).Color.A == 0,
                        "Clicking the selected outlined control again must clear its filter and highlights.");
                    f.Click(f.Arrows); Require(f.Filter.Id == f.Id, "The cleared control cannot be selected again.");
                    f.Filter.Clear();
                    f.Badge.Update(new BadgeModel(f.Report, f.Id, f.Filter.Id), true, new ModText("ja")); f.Layout(800);
                    Require(Same(runs[0].Foreground, normalUp) && Same(runs[2].Foreground, normalDown) &&
                        ((SolidColorBrush)selection.BorderBrush).Color.A == 0,
                        "The existing Show all action must still reset the arrow control.");
                });
                Test("removing and recreating row badges restores the original layout and handlers", () =>
                {
                    using var f = new Fixture();
                    f.Badge.Update(new BadgeModel(f.Report, f.Id, null), true, new ModText("en")); f.Layout(800);
                    f.Badge.Dispose();
                    Require(f.Row.ColumnDefinitions.Count == 9 && f.Row.Children.Count == f.Original.Count &&
                        f.Original.All(p => f.Row.Children.Contains(p.Key) && Grid.GetColumn(p.Key) == p.Value),
                        "Detaching the badges changed original controls or left status columns behind.");
                    f.Badge = new RowBadge(f.Row, f.Source, _ => f.Filters++);
                    f.Badge.Update(new BadgeModel(f.Report, f.Id, null), true, new ModText("en")); f.Layout(800);
                    Require(f.Row.ColumnDefinitions.Count == 11 && f.Row.Children.Count == f.Original.Count + 2,
                        "Recreating a row duplicated badge controls or columns.");
                    f.Click(f.Arrows); Require(f.Filters == 1, "A recycled row retained an obsolete filter handler.");
                });
                Console.WriteLine($"Passed {passed} native UI checks without launching or attaching to Sea Power.");
            }
            finally { GUI.Shutdown(); }
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly Grid Row = new() { VerticalAlignment = VerticalAlignment.Top, Height = 40 };
        internal readonly SearchDirectory Source;
        internal readonly TextBlock Anchor = new() { Text = "A", Margin = new Thickness(4, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center };
        internal readonly CheckBox Check;
        internal readonly Button Workshop;
        internal readonly TextBlock Local = new() { Text = "Local", VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed };
        internal readonly ConflictReport Report;
        internal readonly ModFilter Filter = new();
        internal readonly TextBlock NameLabel = new() { Text = "Example mod", VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(216, 216, 216)) };
        internal readonly Dictionary<UIElement, int> Original;
        internal RowBadge Badge;
        internal int Filters, NativeClicks;
        private readonly View view;
        private double time;
        internal string Id => Source.DirectoryInfo.FullName;
        internal Border Dll => Row.Children.OfType<Border>().Single(b => b.Name == "ModManagerEnhancementsDllBadge");
        internal Button Arrows => Row.Children.OfType<Button>().Single(b => b.Name == "ModManagerEnhancementsOverrides");

        internal Fixture()
        {
#pragma warning disable SYSLIB0050
            Source = (SearchDirectory)FormatterServices.GetUninitializedObject(typeof(SearchDirectory));
#pragma warning restore SYSLIB0050
            typeof(SearchDirectory).GetField("_directoryInfo", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(Source, new DirectoryInfo(Path.Combine(Path.GetTempPath(), "ModManagerEnhancements-ui-source")));
            typeof(SearchDirectory).GetField("<Name>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(Source, "Example mod");
            for (int i = 0; i < 9; i++) Row.ColumnDefinitions.Add(new ColumnDefinition { Width = i == 3 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });
            var style = (Style)GUI.ParseXaml("<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Border Background='#303A41'><ContentPresenter Margin='{TemplateBinding Padding}'/></Border></ControlTemplate></Setter.Value></Setter></Style>");
            Add(new Button { Content = "up", Style = style, Width = 28 }, 0);
            Add(new Button { Content = "down", Style = style, Width = 28 }, 1);
            Check = new CheckBox { Width = 18, Height = 18, Template = (ControlTemplate)GUI.ParseXaml("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='CheckBox'><Border Background='#303A41'/></ControlTemplate>") };
            Add(Check, 2); Add(NameLabel, 3);
            Add(Anchor, 4); Add(new TextBlock { Text = "!", Width = 16 }, 6);
            Workshop = new Button { Content = "Workshop", Style = style, Padding = new Thickness(4) };
            Workshop.Click += (_, _) => NativeClicks++; Add(Workshop, 8); Add(Local, 8);
            Original = Row.Children.Cast<UIElement>().ToDictionary(c => c, Grid.GetColumn);
            var own = new ModSource(Id, "Own", Id); var other = new ModSource("other", "Other", "other");
            Report = new ConflictReport(new[] {
                new FileOverlap("a.png", new[] { new FileProvider(own, "a", "own"), new FileProvider(other, "a", "other") }),
                new FileOverlap("b.png", new[] { new FileProvider(other, "b", "other"), new FileProvider(own, "b", "own") }) });
            Badge = new RowBadge(Row, Source, Select);
            view = GUI.CreateView(Row);
        }

        private void Select(SearchDirectory source)
        {
            Filters++; Filter.Toggle(source.DirectoryInfo.FullName, source.Name);
            Badge.Update(new BadgeModel(Report, Id, Filter.Id), true, new ModText("en"));
        }
        private void Add(UIElement child, int column) { Grid.SetColumn(child, column); Row.Children.Add(child); }
        internal void Layout(int width) { view.SetSize(width, 100); view.Update(time += 0.1); }
        internal Rect Bounds(FrameworkElement element) => element.TransformToAncestor(Row).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
        internal void Click(FrameworkElement element)
        {
            var bounds = Bounds(element); int x = (int)(bounds.X + bounds.Width / 2), y = (int)(bounds.Y + bounds.Height / 2);
            view.MouseMove(x, y); view.MouseButtonDown(x, y, MouseButton.Left); view.MouseButtonUp(x, y, MouseButton.Left);
        }
        public void Dispose() { Badge.Dispose(); view.Dispose(); }
    }
}

namespace ModManagerEnhancements
{
    // The UI fixture tests row interactions without initializing the game's plugin or logger.
    internal static class Plugin
    {
        internal static void Guard(Action action) => action();
    }
}
