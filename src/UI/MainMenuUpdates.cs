using System;
using System.Collections.Generic;
using System.Linq;
using Noesis;

namespace ModManagerEnhancements;

internal sealed class MainMenuUpdates : IDisposable
{
    private readonly FrameworkElement view;
    private readonly Func<bool> hasUpdates;
    private readonly Action<Exception> logError;
    private NativeProperty? originalTemplate;
    private static DataTemplate? template;

    internal MainMenuUpdates(FrameworkElement view, Func<bool> hasUpdates, Action<Exception> logError)
    {
        this.view = view; this.hasUpdates = hasUpdates; this.logError = logError;
        view.Loaded += Loaded;
        view.Unloaded += Unloaded;
        if (view.IsLoaded) Attach();
    }

    private void Loaded(object sender, RoutedEventArgs args) => Attach();
    private void Unloaded(object sender, RoutedEventArgs args) => Restore();

    private void Attach()
    {
        if (originalTemplate != null) return;
        try
        {
            if (!hasUpdates()) return;
            var matches = new List<Button>();
            Visit(view, matches);
            var button = matches.SingleOrDefault()
                ?? throw new InvalidOperationException("The main menu's MOD manager button is missing.");
            var updatedTemplate = template ??= GUI.ParseXaml(
                "<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>" +
                "<StackPanel Orientation='Horizontal' IsHitTestVisible='False'>" +
                "<TextBlock Text='{Binding}' VerticalAlignment='Center'/>" +
                "<Ellipse Name='ModManagerEnhancementsMenuUpdate' Width='10' Height='10' " +
                "Margin='8,0,0,0' VerticalAlignment='Center' Fill='#7ED9F2'/>" +
                "</StackPanel></DataTemplate>") as DataTemplate
                ?? throw new InvalidOperationException("The menu update template is not a DataTemplate.");
            // Preserve the original localized content binding, style, focus and click handler.
            originalTemplate = new NativeProperty(button, ContentControl.ContentTemplateProperty);
            button.ContentTemplate = updatedTemplate;
        }
        catch (Exception error) { Restore(); logError(error); }
    }

    private static void Visit(DependencyObject node, List<Button> matches)
    {
        if (node is Button button && BindingOperations.GetBinding(button, ContentControl.ContentProperty)?.Path?.Path
            == "LanguageResourceHandler.LanguageDictionary[mainmenumods]") matches.Add(button);
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            Visit(VisualTreeHelper.GetChild(node, i), matches);
    }

    private void Restore()
    {
        originalTemplate?.Restore();
        originalTemplate = null;
    }

    public void Dispose()
    {
        view.Loaded -= Loaded;
        view.Unloaded -= Unloaded;
        Restore();
    }
}
