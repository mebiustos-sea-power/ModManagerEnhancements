using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using ModManagerEnhancements;
using Noesis;

internal static class MainMenuUpdateTests
{
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private static bool Near(float a, float b) => Math.Abs(a - b) < 0.5f;

    internal static void Run(Action<string, Action> test)
    {
        test("main-menu update dots follow localized content without changing native clicks or neighboring buttons", () =>
        {
            using var f = new Fixture();
            float optionsWidth = f.Options.ActualWidth;
            var binding = BindingOperations.GetBindingBase(f.Mods, ContentControl.ContentProperty);
            using var indicator = new MainMenuUpdates(f.Menu, () => true, f.Errors.Add);
            foreach (string language in new[] { "en", "ja", "en" })
            foreach (int width in new[] { 480, 800 })
            {
                f.State.LanguageResourceHandler.SetLanguage(language); f.Layout(width);
                var dot = f.Dots().Single();
                var label = f.Elements<TextBlock>(f.Mods).Single();
                var dotBounds = f.Bounds(dot); var labelBounds = f.Bounds(label);
                Require(label.Text == (string)f.Mods.Content && Near(dotBounds.Width, 10) && Near(dotBounds.Height, 10) &&
                    Near(dotBounds.X - (labelBounds.X + labelBounds.Width), 8) &&
                    Near(dotBounds.Y + dotBounds.Height / 2, labelBounds.Y + labelBounds.Height / 2),
                    "The dot is clipped, misplaced, or detached from the localized caption.");
                Require(ReferenceEquals(binding, BindingOperations.GetBindingBase(f.Mods, ContentControl.ContentProperty)) &&
                    Near(f.Options.ActualWidth, optionsWidth) && f.Options.ContentTemplate == null && f.Exit.ContentTemplate == null,
                    "The notification replaced the native content binding or changed other menu buttons.");
                f.Click(dot); f.Click(label);
            }
            Require(f.Clicks == 12 && f.Errors.Count == 0, "The dot/text intercepted the native MOD manager click or a menu error was hidden.");
            indicator.Dispose(); f.Layout();
            Require(f.Mods.ContentTemplate == null && !f.Dots().Any(), "Detaching did not restore the native caption template.");
        });
        test("main-menu notification disappears on return after acknowledgement and stays hidden on later visits", () =>
        {
            using var f = new Fixture();
            int queries = 0;
            bool unseen = true;
            var indicator = new MainMenuUpdates(f.Menu, () => { queries++; return unseen; }, f.Errors.Add);
            f.Layout();
            for (int i = 0; i < 3; i++)
            {
                f.Root.Children.Remove(f.Menu); f.Layout();
                Require(f.Mods.ContentTemplate == null, "Unloading left a modified native button behind.");
                f.Root.Children.Add(f.Menu); f.Layout();
                Require(f.Dots().Count() == 1 && f.Dots().Single().ActualWidth > 0 && f.Dots().Single().ActualHeight > 0, "Returning to the menu consumed, duplicated or delayed the first-frame notification.");
            }
            Require(queries == 4 && f.Errors.Count == 0, "The menu polled update information on unrelated layouts or repeated its load handler.");
            f.Root.Children.Remove(f.Menu); f.Layout();
            unseen = false;
            f.Root.Children.Add(f.Menu); f.Layout();
            Require(!f.Dots().Any() && f.Mods.ContentTemplate == null && queries == 5,
                "Returning from the manager kept the acknowledged menu notification.");
            indicator.Dispose();
            f.Root.Children.Remove(f.Menu); f.Layout(); f.Root.Children.Add(f.Menu); f.Layout();
            Require(!f.Dots().Any() && queries == 5, "Disposal left an active menu handler.");
            using var reopened = new MainMenuUpdates(f.Menu, () => unseen, f.Errors.Add);
            f.Layout(); Require(!f.Dots().Any(), "Reopening restored an already acknowledged notification.");
            using var recreated = new Fixture();
            using var newMenu = new MainMenuUpdates(recreated.Menu, () => unseen, recreated.Errors.Add);
            recreated.Layout(); Require(!recreated.Dots().Any(), "A newly created main menu restored an acknowledged notification.");
        });
        test("unchanged main menus retain native content and missing MOD buttons report an error", () =>
        {
            using var f = new Fixture();
            using (var unchanged = new MainMenuUpdates(f.Menu, () => false, f.Errors.Add))
            {
                f.Layout(); Require(!f.Dots().Any() && f.Mods.ContentTemplate == null, "An unchanged menu reserved notification space.");
            }
            BindingOperations.ClearBinding(f.Mods, ContentControl.ContentProperty); f.Mods.Content = "Other button";
            using var invalid = new MainMenuUpdates(f.Menu, () => true, f.Errors.Add);
            f.Layout();
            Require(f.Errors.Count == 1 && !f.Dots().Any() && f.Mods.ContentTemplate == null,
                "An unsupported menu silently failed or decorated a button based on its displayed text.");
        });
    }

    private sealed class Fixture : IDisposable
    {
        internal Grid Root { get; } = new();
        internal UserControl Menu { get; }
        internal Button Mods { get; }
        internal Button Options { get; }
        internal Button Exit { get; }
        internal MenuState State { get; } = new();
        internal List<Exception> Errors { get; } = new();
        internal int Clicks;
        private readonly View view;
        private double time;

        internal Fixture()
        {
            // Native MenuButton uses this content presenter, alignment and transparent click surface.
            Menu = (UserControl)GUI.ParseXaml(@"<UserControl xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
              <UserControl.Resources><Style x:Key='MenuButton' TargetType='Button'>
                <Setter Property='HorizontalContentAlignment' Value='Left'/><Setter Property='VerticalContentAlignment' Value='Center'/>
                <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Grid><Rectangle Fill='Transparent'/><ContentPresenter x:Name='PART_ContentPresenter' HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='{TemplateBinding VerticalContentAlignment}' Margin='{TemplateBinding Padding}'/></Grid></ControlTemplate></Setter.Value></Setter>
              </Style></UserControl.Resources>
              <StackPanel HorizontalAlignment='Left' VerticalAlignment='Top' Margin='30'>
                <Border Width='300' Height='20'/>
                <Button x:Name='Options' Style='{StaticResource MenuButton}' FontSize='25' Content='OPTIONS'/>
                <Button x:Name='Mods' Style='{StaticResource MenuButton}' FontSize='25' Content='{Binding LanguageResourceHandler.LanguageDictionary[mainmenumods]}'/>
                <Button x:Name='Exit' Style='{StaticResource MenuButton}' FontSize='25' Content='QUIT GAME'/>
              </StackPanel></UserControl>");
            Menu.DataContext = State;
            Mods = (Button)Menu.FindName("Mods"); Options = (Button)Menu.FindName("Options"); Exit = (Button)Menu.FindName("Exit");
            Mods.Click += (_, _) => Clicks++;
            Root.Children.Add(Menu); view = GUI.CreateView(Root); Layout();
        }
        internal IEnumerable<T> Elements<T>(DependencyObject root) where T : DependencyObject
        {
            if (root is T match) yield return match;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
                foreach (var child in Elements<T>(VisualTreeHelper.GetChild(root, i))) yield return child;
        }
        internal IEnumerable<Ellipse> Dots() => Elements<Ellipse>(Menu);
        internal Rect Bounds(FrameworkElement element) => element.TransformToAncestor(Root).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
        internal void Layout(int width = 800) { view.SetSize(width, 600); view.Update(time += 0.1); }
        internal void Click(FrameworkElement element)
        {
            Rect b = Bounds(element); int x = (int)(b.X + b.Width / 2), y = (int)(b.Y + b.Height / 2);
            view.MouseMove(x, y); view.MouseButtonDown(x, y, MouseButton.Left); view.MouseButtonUp(x, y, MouseButton.Left);
        }
        public void Dispose() { view.Dispose(); }
    }
    public sealed class MenuState
    {
        public MenuLanguage LanguageResourceHandler { get; } = new();
    }
    public sealed class MenuLanguage : INotifyPropertyChanged
    {
        public Dictionary<string, string> LanguageDictionary { get; private set; } = new() { ["mainmenumods"] = "MOD MANAGER" };
        public event PropertyChangedEventHandler? PropertyChanged;
        public void SetLanguage(string language)
        {
            LanguageDictionary = new Dictionary<string, string> { ["mainmenumods"] = language == "ja" ? "MODマネージャー" : "MOD MANAGER" };
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LanguageDictionary)));
        }
    }
}
