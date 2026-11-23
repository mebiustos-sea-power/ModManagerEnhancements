using System;
using System.Collections.Generic;
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

internal static class Program
{
    private static int passed;
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Test(string name, Action action) { action(); passed++; Console.WriteLine("PASS " + name); }
    private static bool Near(float a, float b) => Math.Abs(a - b) < 0.5f;
    private static bool Same(Brush a, Brush b)
    {
        var x = ((SolidColorBrush)a).Color; var y = ((SolidColorBrush)b).Color;
        return x.A == y.A && x.R == y.R && x.G == y.G && x.B == y.B;
    }

    private static int Main()
    {
        try
        {
            if (Type.GetType("Mono.Runtime") == null) throw new PlatformNotSupportedException("Use the standalone game Mono runner.");
            GUI.Init();
            try
            {
                Test("the native shared source button shows aligned icons and fixed rightmost arrows", () =>
                {
                    using var f = new Fixture();
                    var reports = new[] { f.Counts(1, 0), f.Report, f.Counts(0, 1), f.Counts(9, 9), f.Counts(10, 123) };
                    foreach (int width in new[] { 480, 800, 1200 })
                        foreach (float font in new[] { 14f, 20f })
                        {
                            Rect? sourceBaseline = null, arrowBaseline = null; float? upBaseline = null, downBaseline = null;
                            f.Row.SetValue(TextElement.FontSizeProperty, font);
                            foreach (bool warning in new[] { false, true })
                            foreach (bool anchor in new[] { false, true })
                                foreach (bool steam in new[] { false, true })
                                    foreach (string language in new[] { "ja", "en" })
                                        foreach (var report in reports)
                                        {
                                            f.Warning.Visibility = warning ? Visibility.Visible : Visibility.Collapsed;
                                            f.Anchor.Visibility = anchor ? Visibility.Visible : Visibility.Collapsed;
                                            f.SetOrigin(steam); f.State.SetLanguage(language);
                                            f.Badge.Update(new BadgeModel(report, f.Id, null), true, TestText.Create(language)); f.Layout(width);
                                            var dll = f.Bounds(f.Dll); var origin = f.Bounds(f.Origin); var arrows = f.Bounds(f.Arrows);
                                            var up = f.Bounds(f.Up); var down = f.Bounds(f.Down);
                                            var dllText = f.Bounds((TextBlock)f.Dll.Child);
                                            if (!warning)
                                                Require(origin.X - (dllText.X + dllText.Width) <= 10.5f,
                                                    "Hidden dependency warnings must not leave excessive space after DLL.");
                                            else
                                            {
                                                var dependency = f.Bounds(f.Warning);
                                                Require(dll.X + dll.Width <= dependency.X && dependency.X + dependency.Width <= origin.X,
                                                    "Compacting the DLL gap must keep native dependency warnings unobstructed.");
                                            }
                                            Require(dll.Width > 0 && dll.X + dll.Width <= origin.X && origin.X + origin.Width <= arrows.X
                                                && Near(arrows.X + arrows.Width, f.Row.ActualWidth - f.Arrows.Margin.Right),
                                                "Source indicators must precede fixed arrows at the actual right edge.");
                                            Require(ReferenceEquals(f.Origin.Content, f.Caption) && f.Origin.IsEnabled == steam,
                                                "The native shared source button must retain its caption and enabled-state binding.");
                                            if (steam)
                                            {
                                                var image = f.Icon as Image;
                                                Require(image != null && ((BitmapSource)image.Source).PixelWidth == 48,
                                                    "Workshop origins must use the embedded official Steam image.");
                                            }
                                            else Require(f.Icon is Noesis.Path, "Local origins must use a folder geometry in the same native button.");
                                            if (sourceBaseline.HasValue)
                                                Require(Near(origin.X, sourceBaseline.Value.X) && Near(origin.Width, sourceBaseline.Value.Width)
                                                    && Near(arrows.X, arrowBaseline!.Value.X) && Near(arrows.Width, arrowBaseline.Value.Width)
                                                    && Near(up.X, upBaseline!.Value) && Near(down.X, downBaseline!.Value),
                                                    "Source kind, language, status visibility or counts shifted the reserved slots.");
                                            sourceBaseline = origin; arrowBaseline = arrows; upBaseline = up.X; downBaseline = down.X;
                                            Require(Near(up.Width, down.Width) && up.X + up.Width <= down.X,
                                                "The direction slots must be separate and equal.");
                                            foreach (var label in new[] { f.Up, f.Down })
                                            {
                                                var probe = new TextBlock { Text = label.Text, FontFamily = label.FontFamily, FontSize = label.FontSize };
                                                probe.Measure(new Size(float.PositiveInfinity, float.PositiveInfinity));
                                                Require(probe.DesiredSize.Width <= label.ActualWidth + 0.5f, "A compact count was clipped.");
                                            }
                                            if (anchor)
                                            {
                                                var a = f.Bounds(f.Anchor);
                                                Require(Near(dllText.X - (a.X + a.Width), 10) && dll.X + dll.Width <= origin.X,
                                                    "The native A mark must keep its original spacing before DLL.");
                                            }
                                            f.Badge.Update(new BadgeModel(null, f.Id, null), true, TestText.Create(language)); f.Layout(width);
                                            Require(f.Arrows.Visibility == Visibility.Collapsed && Near(f.Bounds(f.Origin).X, origin.X),
                                                "Rows without overrides must reserve the same column widths.");
                                        }
                        }
                    f.Source.IsChecked = false;
                    f.Badge.Update(new BadgeModel(null, f.Id, null), true, TestText.Create("ja")); f.Layout(800);
                    Require(f.Dll.Visibility == Visibility.Visible && f.Arrows.Visibility == Visibility.Collapsed,
                        "Disabled/conflict-free DLL sources must retain their independent indicator.");
                    f.Badge.Update(new BadgeModel(f.Report, f.Id, null), false, TestText.Create("en")); f.Layout(800);
                    Require(f.Dll.Visibility == Visibility.Collapsed && f.Arrows.Visibility == Visibility.Visible,
                        "Data-only sources lost their independent override control.");
                });
                Test("native Workshop commands, disabled Local tooltips and filter toggles remain usable", () =>
                {
                    using var f = new Fixture();
                    f.SetOrigin(true); f.Badge.Update(new BadgeModel(f.Report, f.Id, null), true, TestText.Create("en")); f.Layout(800);
                    var dll = (TextBlock)f.Dll.Child;
                    var dllColor = ((SolidColorBrush)dll.Foreground).Color;
                    var nameColor = ((SolidColorBrush)f.NameLabel.Foreground).Color;
                    Require(f.Dll.BorderThickness.Left + f.Dll.BorderThickness.Top + f.Dll.BorderThickness.Right + f.Dll.BorderThickness.Bottom == 0
                        && dll.FontSize < f.NameLabel.FontSize && Math.Max(dllColor.R, Math.Max(dllColor.G, dllColor.B)) < Math.Min(nameColor.R, Math.Min(nameColor.G, nameColor.B)),
                        "DLL information must remain small, muted and borderless.");
                    var selection = (Border)f.Arrows.Content;
                    Brush normalUp = f.Up.Foreground, normalDown = f.Down.Foreground;
                    f.Click(f.Dll); Require(f.Filters == 0, "DLL information triggered a filter.");
                    Require(f.HoverSourceCenter() > 0, "Hovering the Steam icon center must request its source tooltip.");
                    f.Click(f.Origin);
                    Require(f.NativeClicks == 1 && f.Filters == 0 && ReferenceEquals(f.Origin.Command, f.State.OpenSteamPageCommand),
                        "The Steam icon lost or replaced the native Workshop command.");
                    f.SetOrigin(false); f.Badge.Update(new BadgeModel(f.Report, f.Id, null), true, TestText.Create("en")); f.Layout(800);
                    Require(f.HoverSourceCenter() > 0, "Hovering the empty folder center must request its tooltip while the local button stays disabled.");
                    f.Click(f.Origin);
                    Require(f.NativeClicks == 1 && f.Filters == 0 && !f.Origin.IsEnabled
                        && ToolTipService.GetShowOnDisabled(f.Origin) && (string)f.Origin.ToolTip == "Local mod",
                        "The native disabled Local indicator must retain a tooltip without executing a command.");
                    f.Badge.Update(new BadgeModel(f.Report, f.Id, null), true, TestText.Create("ja")); f.Layout(800);
                    Require((string)f.Origin.ToolTip == "ローカルMOD", "The Local tooltip lost its localized source description.");
                    f.SetOrigin(true); f.Badge.Update(new BadgeModel(f.Report, f.Id, null), true, TestText.Create("ja")); f.Layout(800);
                    Require((string)f.Origin.ToolTip == "Steamワークショップ", "The Workshop tooltip lost its localized source description.");
                    f.Click(f.Check); Require(f.Check.IsChecked == true && f.Filters == 0, "The native enable checkbox was affected.");
                    f.Click(f.Arrows); f.Layout(800);
                    Require(f.Filters == 1 && f.Filter.Id == f.Id && !Same(f.Up.Foreground, normalUp) && !Same(f.Down.Foreground, normalDown)
                        && ((SolidColorBrush)selection.BorderBrush).Color.A > 0, "Arrow selection/highlighting failed.");
                    f.Click(f.Arrows); f.Layout(800);
                    Require(f.Filters == 2 && f.Filter.Id == null && Same(f.Up.Foreground, normalUp) && Same(f.Down.Foreground, normalDown)
                        && ((SolidColorBrush)selection.BorderBrush).Color.A == 0, "Clicking the selected badge again must clear its filter.");
                    f.Click(f.Arrows); Require(f.Filter.Id == f.Id, "The cleared badge could not be selected again.");
                    f.Filter.Clear(); f.Badge.Update(new BadgeModel(f.Report, f.Id, null), true, TestText.Create("ja")); f.Layout(800);
                    Require(Same(f.Up.Foreground, normalUp) && Same(f.Down.Foreground, normalDown), "Show all did not clear highlighting.");
                });
                Test("version warnings remain visible on disabled rows and preserve neighboring controls", () =>
                {
                    string tooltip = TestText.Create("ja").CompatibilityTooltip;
                    using var baseline = new Fixture();
                    using var f = new Fixture(compatibilityWarning: tooltip);
                    f.SetOrigin(true);
                    baseline.SetOrigin(true);
                    f.Warning.Visibility = baseline.Warning.Visibility = Visibility.Visible;
                    f.Anchor.Visibility = baseline.Anchor.Visibility = Visibility.Visible;
                    var warning = f.CompatibilityWarning ?? throw new Exception("The row did not show its version warning.");
                    Require(baseline.CompatibilityWarning == null, "The row showed a warning without a version mismatch.");

                    foreach (int width in new[] { 480, 800 })
                    {
                        foreach (bool enabled in new[] { true, false })
                        {
                            f.Source.IsChecked = enabled;
                            f.Check.IsChecked = enabled;
                            f.Badge.Update(new BadgeModel(f.Report, f.Id, null), true, TestText.Create("ja"));
                            baseline.Badge.Update(new BadgeModel(baseline.Report, baseline.Id, null), true, TestText.Create("ja"));
                            f.Layout(width);
                            baseline.Layout(width);
                            var mark = f.Bounds(warning);
                            var anchor = f.Bounds(f.Anchor);
                            Require(mark.Width > 0 && mark.X + mark.Width <= anchor.X,
                                "The compatibility warning must be left of the native A mark.");
                            Require(Near(anchor.X, baseline.Bounds(baseline.Anchor).X) &&
                                Near(f.Bounds(f.Dll).X, baseline.Bounds(baseline.Dll).X) &&
                                Near(f.Bounds(f.Warning).X, baseline.Bounds(baseline.Warning).X) &&
                                Near(f.Bounds(f.Origin).X, baseline.Bounds(baseline.Origin).X) &&
                                Near(f.Bounds(f.Arrows).X, baseline.Bounds(baseline.Arrows).X),
                                "The compatibility warning shifted an existing indicator.");
                            Require(Near(f.Bounds(f.NameLabel).X, baseline.Bounds(baseline.NameLabel).X) &&
                                baseline.Row.ColumnDefinitions.Count == 11 &&
                                f.NameLabel.ActualWidth < baseline.NameLabel.ActualWidth,
                                "Only warned rows should spend name space on the warning.");
                            Require(warning.ToolTip is string tip && tip == tooltip && f.HoverCenter(warning) > 0,
                                "Hovering the compatibility warning must show its tooltip.");
                            f.Click(warning);
                            Require(f.Check.IsChecked == enabled && f.NativeClicks == 0 && f.Filters == 0,
                                "Clicking the warning changed the enabled state or triggered a command.");
                        }
                    }

                    f.Badge.Dispose();
                    f.Layout(480);
                    Require(f.CompatibilityWarning == null && f.Row.Children.Count == f.Original.Count &&
                        f.Row.ColumnDefinitions.Count == 9 && f.Original.All(p => Grid.GetColumn(p.Key) == p.Value),
                        "Detaching the row left its compatibility warning behind.");
                });
                Test("changed-version pencils share the warning slot and remain passive", () =>
                {
                    using var changed = new Fixture(compatibilityModified: true);
                    using var warned = new Fixture(compatibilityWarning: TestText.Create("ja").CompatibilityTooltip, compatibilityModified: true);
                    var pencil = changed.Row.Children.OfType<Border>().Single(b => b.Name == "ModManagerEnhancementsCompatibilityOverride");
                    foreach (string language in new[] { "ja", "en" })
                        foreach (int width in new[] { 480, 800 })
                        {
                            var text = TestText.Create(language);
                            changed.SetOrigin(true); warned.SetOrigin(true);
                            changed.Anchor.Visibility = warned.Anchor.Visibility = Visibility.Visible;
                            changed.Badge.Update(new BadgeModel(changed.Report, changed.Id, null), true, text);
                            warned.Badge.Update(new BadgeModel(warned.Report, warned.Id, null), true, text);
                            changed.Layout(width); warned.Layout(width);
                            Require(changed.CompatibilityWarning == null && warned.CompatibilityWarning != null &&
                                !warned.Row.Children.OfType<Border>().Any(b => b.Name == "ModManagerEnhancementsCompatibilityOverride"),
                                "The warning did not take priority over the pencil.");
                            Require(Near(changed.Bounds(changed.Anchor).X, warned.Bounds(warned.Anchor).X) &&
                                Near(changed.Bounds(changed.Origin).X, warned.Bounds(warned.Origin).X) &&
                                Near(changed.Bounds(changed.Arrows).X, warned.Bounds(warned.Arrows).X),
                                "The pencil used different spacing from the warning.");
                            Require((string)pencil.ToolTip == text.CompatibilityModifiedTooltip && changed.HoverCenter(pencil) > 0,
                                "The changed-version tooltip did not follow language or pointer position.");
                            bool? active = changed.Check.IsChecked;
                            changed.Click(pencil);
                            Require(changed.Check.IsChecked == active && changed.NativeClicks == 0 && changed.Filters == 0,
                                "The status icon performed an unintended action.");
                        }
                    changed.Badge.Dispose(); changed.Layout(480);
                    Require(changed.Row.ColumnDefinitions.Count == 9 && changed.Row.Children.Count == changed.Original.Count,
                        "Detaching left the pencil or its column behind.");
                });
                Test("Workshop update dots stay leftmost passive and do not reserve space in unchanged rows", () =>
                {
                    foreach (string status in new[] { "none", "warning", "pencil" })
                    {
                        string? warning = status == "warning" ? TestText.Create("ja").CompatibilityTooltip : null;
                        using var baseline = new Fixture(compatibilityWarning: warning, compatibilityModified: status == "pencil");
                        using var f = new Fixture(compatibilityWarning: warning, compatibilityModified: status == "pencil", workshopUpdated: true);
                        var dot = f.Row.Children.OfType<Border>().Single(b => b.Name == "ModManagerEnhancementsWorkshopUpdate");
                        f.SetOrigin(true); baseline.SetOrigin(true);
                        f.Anchor.Visibility = baseline.Anchor.Visibility = Visibility.Visible;
                        foreach (string language in new[] { "ja", "en" })
                        foreach (int width in new[] { 480, 800 })
                        foreach (bool enabled in new[] { false, true })
                        {
                            var text = TestText.Create(language);
                            f.Check.IsChecked = enabled;
                            f.Badge.Update(new BadgeModel(f.Report, f.Id, null), true, text);
                            baseline.Badge.Update(new BadgeModel(baseline.Report, baseline.Id, null), true, text);
                            f.Layout(width); baseline.Layout(width);
                            var next = f.Row.Children.OfType<Border>().FirstOrDefault(b =>
                                b.Name == "ModManagerEnhancementsCompatibilityWarning" || b.Name == "ModManagerEnhancementsCompatibilityOverride");
                            var nextBounds = next == null ? f.Bounds(f.Anchor) : f.Bounds(next);
                            var bounds = f.Bounds(dot);
                            Require(bounds.Width > 0 && bounds.X + bounds.Width <= nextBounds.X,
                                "The update dot overlaps or follows another status icon.");
                            Require(Near(f.Bounds(f.Anchor).X, baseline.Bounds(baseline.Anchor).X) &&
                                Near(f.Bounds(f.Dll).X, baseline.Bounds(baseline.Dll).X) &&
                                Near(f.Bounds(f.Origin).X, baseline.Bounds(baseline.Origin).X) &&
                                Near(f.Bounds(f.Arrows).X, baseline.Bounds(baseline.Arrows).X) &&
                                f.Row.ColumnDefinitions.Count == baseline.Row.ColumnDefinitions.Count + 1 &&
                                f.NameLabel.ActualWidth < baseline.NameLabel.ActualWidth,
                                "The dot changed trailing controls or reserved space in an unchanged row.");
                            Require((string)dot.ToolTip == text.WorkshopUpdatedTooltip && f.HoverCenter(dot) > 0,
                                "The update dot did not expose its localized tooltip.");
                            f.Click(dot);
                            Require(f.Check.IsChecked == enabled && f.Filters == 0 && f.NativeClicks == 0,
                                "The passive update dot performed an action.");
                        }
                        f.Badge.Dispose();
                        f.Badge = new RowBadge(f.Row, f.Source, _ => f.Filters++, f.Metrics, warning, status == "pencil");
                        f.Badge.Update(new BadgeModel(f.Report, f.Id, null), true, TestText.Create("ja"));
                        f.Layout(480);
                        Require(!f.Row.Children.OfType<Border>().Any(b => b.Name == "ModManagerEnhancementsWorkshopUpdate") &&
                            f.Row.ColumnDefinitions.Count == baseline.Row.ColumnDefinitions.Count,
                            "Reusing a row left a consumed update dot or its column behind.");
                    }
                });
                Test("detaching restores the native shared caption, source bindings and layout", () =>
                {
                    using var f = new Fixture();
                    f.SetOrigin(true); f.Badge.Update(new BadgeModel(f.Report, f.Id, null), true, TestText.Create("en")); f.Layout(800);
                    var bitmap = ((Image)f.Icon).Source;
                    var compactedNameWidth = f.NameLabel.ActualWidth;
                    f.Badge.Dispose(); f.State.SetLanguage("ja"); f.Layout(800);
                    Require(f.Row.ColumnDefinitions.Count == 9 && f.Row.Children.Count == f.Original.Count
                        && f.Original.All(p => f.Row.Children.Contains(p.Key) && Grid.GetColumn(p.Key) == p.Value),
                        "Detaching left controls or columns behind.");
                    Require(Near(f.Row.ColumnDefinitions[5].ActualWidth, 5) && Near(f.Row.ColumnDefinitions[7].ActualWidth, 5)
                        && f.NameLabel.ActualWidth > compactedNameWidth, "Detaching must restore native spacing and return row space to the mod name.");
                    Require(ReferenceEquals(f.Origin.Content, f.Caption) && f.Caption.Text == "ワークショップ"
                        && f.Origin.ContentTemplate == null && float.IsNaN(f.Origin.Width) && f.Row.ColumnDefinitions[8].Width.IsAuto
                        && !ToolTipService.GetShowOnDisabled(f.Origin), "The native caption, properties or localization were not restored.");
                    f.SetOrigin(false); f.Layout(800);
                    Require(!f.Origin.IsEnabled && f.Caption.Text == "ローカル", "The native IsSteam trigger stopped updating after detachment.");
                    f.Badge = new RowBadge(f.Row, f.Source, _ => f.Filters++, f.Metrics);
                    f.SetOrigin(true); f.Badge.Update(new BadgeModel(f.Report, f.Id, null), true, TestText.Create("ja")); f.Layout(800);
                    Require(f.Row.Children.Count == f.Original.Count + 2 && ReferenceEquals(((Image)f.Icon).Source, bitmap),
                        "Recreation duplicated controls or reloaded the shared bitmap.");
                    f.Click(f.Origin); f.Click(f.Arrows);
                    Require(f.NativeClicks == 1 && f.Filters == 1, "Recreation lost or duplicated native/filter handlers.");
                    using var resource = new Fixture(resourceCaption: true);
                    resource.SetOrigin(true); resource.Badge.Update(new BadgeModel(resource.Report, resource.Id, null), false, TestText.Create("en")); resource.Layout(800);
                    resource.Badge.Dispose(); resource.Row.Resources["Caption"] = "Updated Workshop"; resource.Layout(800);
                    Require(resource.Caption.Text == "Updated Workshop", "The original caption's dynamic-resource reference was lost.");
                });
                SearchUiTests.Run(Test);
                CompatibilityUiTests.Run(Test);
                SyncUiTests.Run(Test);
                LocalizationUiTests.Run(Test);
                ControllerUiTests.Run(Test);
                WorkshopUpdateTests.Run(Test);
                MainMenuUpdateTests.Run(Test);
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
        internal readonly TextBlock Warning = new() { Text = "!", Width = 16, Visibility = Visibility.Collapsed, VerticalAlignment = VerticalAlignment.Center };
        internal readonly CheckBox Check;
        internal readonly Button Origin;
        internal readonly TextBlock Caption;
        internal readonly SourceState State;
        internal readonly ConflictReport Report;
        internal readonly ModFilter Filter = new();
        internal readonly RowMetrics Metrics = new();
        internal readonly TextBlock NameLabel = new() { Text = "Example mod", VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush(Color.FromRgb(216, 216, 216)) };
        internal readonly Dictionary<UIElement, int> Original;
        internal RowBadge Badge;
        internal int Filters, NativeClicks;

        private readonly View view;
        private double time;
        internal string Id => Source.DirectoryInfo.FullName;
        internal Border Dll => Row.Children.OfType<Border>().Single(b => b.Name == "ModManagerEnhancementsDllBadge");
        internal Border? CompatibilityWarning => Row.Children.OfType<Border>()
            .SingleOrDefault(b => b.Name == "ModManagerEnhancementsCompatibilityWarning");
        internal Button Arrows => Row.Children.OfType<Button>().Single(b => b.Name == "ModManagerEnhancementsOverrides");
        internal TextBlock Up => ((Grid)((Border)Arrows.Content).Child).Children.OfType<TextBlock>().First();
        internal TextBlock Down => ((Grid)((Border)Arrows.Content).Child).Children.OfType<TextBlock>().Last();
        internal FrameworkElement Icon => FindIcon(Origin) ?? throw new Exception("The shared native source button did not display its icon template.");
        private static FrameworkElement? FindIcon(DependencyObject node)
        {
            if (node is Image image) return image;
            if (node is Noesis.Path path) return path;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            {
                var found = FindIcon(VisualTreeHelper.GetChild(node, i)); if (found != null) return found;
            }
            return null;
        }

        internal Fixture(bool resourceCaption = false, string? compatibilityWarning = null, bool compatibilityModified = false, bool workshopUpdated = false)
        {
#pragma warning disable SYSLIB0050
            Source = (SearchDirectory)FormatterServices.GetUninitializedObject(typeof(SearchDirectory));
#pragma warning restore SYSLIB0050
            typeof(SearchDirectory).GetField("_directoryInfo", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(Source, new DirectoryInfo(Path.Combine(Path.GetTempPath(), "ModManagerEnhancements-ui-source")));
            typeof(SearchDirectory).GetField("<Name>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(Source, "Example mod");
            State = new SourceState(() => NativeClicks++); Row.DataContext = State;
            for (int i = 0; i < 9; i++) Row.ColumnDefinitions.Add(new ColumnDefinition { Width = i == 3 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });
            Row.ColumnDefinitions[5].Width = Row.ColumnDefinitions[7].Width = new GridLength(5);
            // An unfilled template matches native hyperlink hit testing; a filled Border masks icon gaps.
            var style = (Style)GUI.ParseXaml("<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Grid><ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='{TemplateBinding VerticalContentAlignment}' Margin='{TemplateBinding Padding}'/></Grid></ControlTemplate></Setter.Value></Setter></Style>");
            Add(new Button { Content = "up", Style = style, Width = 28 }, 0); Add(new Button { Content = "down", Style = style, Width = 28 }, 1);
            Check = new CheckBox { Width = 18, Height = 18, Template = (ControlTemplate)GUI.ParseXaml("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='CheckBox'><Border Background='#303A41'/></ControlTemplate>") };
            Add(Check, 2); Add(NameLabel, 3); Add(Anchor, 4); Add(Warning, 6);
            // Sea Power 0.8.7: one Button at column 8; a nested TextBlock switches both captions via IsSteam.
            Origin = (Button)GUI.ParseXaml("<Button xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' IsEnabled='{Binding IsSteam}' Command='{Binding OpenSteamPageCommand}' Padding='0' Margin='0' HorizontalAlignment='Right' VerticalAlignment='Center'><TextBlock><TextBlock.Style><Style TargetType='TextBlock'><Style.Triggers><DataTrigger Binding='{Binding IsSteam}' Value='False'><Setter Property='Text' Value='{Binding Local}'/></DataTrigger><DataTrigger Binding='{Binding IsSteam}' Value='True'><Setter Property='Text' Value='{Binding Workshop}'/></DataTrigger></Style.Triggers></Style></TextBlock.Style></TextBlock></Button>");
            Origin.Style = style; Caption = (TextBlock)Origin.Content; Add(Origin, 8);

            if (resourceCaption) { Row.Resources["Caption"] = "Workshop"; Caption.SetResourceReference(TextBlock.TextProperty, "Caption"); }
            Original = Row.Children.Cast<UIElement>().ToDictionary(c => c, Grid.GetColumn);
            Report = Counts(1, 1); Badge = new RowBadge(Row, Source, Select, Metrics, compatibilityWarning, compatibilityModified, workshopUpdated); view = GUI.CreateView(Row);
        }

        internal ConflictReport Counts(int up, int down)
        {
            var own = new ModSource(Id, "Own", Id); var other = new ModSource("other", "Other", "other");
            return new ConflictReport(Enumerable.Range(0, up + down).Select(i => i < up
                ? new FileOverlap(i + ".png", new[] { new FileProvider(own, "own", "own"), new FileProvider(other, "other", "other") })
                : new FileOverlap(i + ".png", new[] { new FileProvider(other, "other", "other"), new FileProvider(own, "own", "own") })).ToArray());
        }
        internal void SetOrigin(bool steam)
        {
            typeof(SearchDirectory).GetField("<IsSteam>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(Source, steam);
            State.IsSteam = steam;
        }
        private void Select(SearchDirectory source)
        {
            Filters++; Filter.Toggle(source.DirectoryInfo.FullName, source.Name);
            Badge.Update(new BadgeModel(Report, Id, Filter.Id), true, TestText.Create("en"));
        }
        private void Add(UIElement child, int column) { Grid.SetColumn(child, column); Row.Children.Add(child); }
        internal void Layout(int width) { view.SetSize(width, 100); view.Update(time += 0.1); }
        internal Rect Bounds(FrameworkElement element) => element.TransformToAncestor(Row).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
        internal int HoverSourceCenter() => HoverCenter(Origin);
        internal int HoverCenter(FrameworkElement element)
        {
            ToolTipService.SetInitialShowDelay(element, 0); ToolTipService.SetBetweenShowDelay(element, 0);
            view.MouseMove(0, 80); view.Update(time += 0.1);
            int requests = 0;
            ToolTipEventHandler opening = (_, _) => requests++;
            element.ToolTipOpening += opening;
            try
            {
                var bounds = Bounds(element);
                view.MouseMove((int)(bounds.X + bounds.Width / 2), (int)(bounds.Y + bounds.Height / 2));
                view.Update(time += 0.1); System.Threading.Thread.Sleep(1); view.Update(time += 0.1);
                return requests;
            }
            finally { element.ToolTipOpening -= opening; }
        }
        internal void Click(FrameworkElement element)
        {
            var b = Bounds(element); int x = (int)(b.X + b.Width / 2), y = (int)(b.Y + b.Height / 2);
            view.MouseMove(x, y); view.MouseButtonDown(x, y, MouseButton.Left); view.MouseButtonUp(x, y, MouseButton.Left);
        }
        public void Dispose() { Badge.Dispose(); view.Dispose(); }
    }

    public sealed class SourceState : INotifyPropertyChanged
    {
        private bool isSteam;
        public bool IsSteam { get => isSteam; set { isSteam = value; Changed("IsSteam"); } }
        public string Workshop { get; private set; } = "Workshop";
        public string Local { get; private set; } = "Local";
        public ICommand OpenSteamPageCommand { get; }
        public SourceState(Action action) { OpenSteamPageCommand = new SourceAction(action); }
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Changed(string property) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        public void SetLanguage(string language)
        {
            Workshop = language == "ja" ? "ワークショップ" : "Workshop"; Local = language == "ja" ? "ローカル" : "Local";
            Changed("Workshop"); Changed("Local");
        }
    }
    private sealed class SourceAction : ICommand
    {
        private readonly Action action;
        internal SourceAction(Action action) { this.action = action; }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => action();
        public event System.EventHandler? CanExecuteChanged { add { } remove { } }
    }
}

namespace ModManagerEnhancements
{
    // The fixture exercises native Noesis controls without starting the game's plugin or logger.
    internal static class Plugin
    {
        internal static ManagerController? Active { get; set; }
        internal static readonly List<Exception> Errors = new();
        internal static bool IsEnabled => throw new NotSupportedException("The fixture does not mount the game view.");
        internal static WorkshopUpdates Updates => throw new NotSupportedException("The fixture does not initialize Workshop state.");
        internal static bool SamePath(string left, string right) => throw new NotSupportedException("The fixture does not mount the game view.");
        internal static void Info(string message) => throw new NotSupportedException("The fixture does not mount the game view.");
        internal static void Error(Exception error) => Errors.Add(error);
        internal static void Guard(Action action) => action();
    }
}
