using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using ModManagerEnhancements.Core;
using Noesis;
using SeaPower;
using EventHandler = System.EventHandler;

namespace ModManagerEnhancements;

internal sealed class ModSortMenu : IDisposable
{
    private readonly Func<ObservableCollection<SearchDirectory>> directories;
    private readonly ModText text;
    private readonly Action<Exception> logError;
    private readonly NativeProperty originalCommand, originalContextMenu, originalTemplate;
    private readonly ContextMenu menu;
    private readonly MenuItem dependencies, enabledFirst;
    private readonly SortAction dependencyAction, groupAction;
    private bool disposed;
    private static DataTemplate? template;

    internal ModSortMenu(Button button, ICommand nativeSort, Func<ObservableCollection<SearchDirectory>> directories,
        ModText text, Action<Exception> logError)
    {
        this.directories = directories; this.text = text; this.logError = logError;
        originalCommand = new NativeProperty(button, Button.CommandProperty);
        originalContextMenu = new NativeProperty(button, FrameworkElement.ContextMenuProperty);
        originalTemplate = new NativeProperty(button, ContentControl.ContentTemplateProperty);
        menu = new ContextMenu { PlacementTarget = button, Placement = PlacementMode.Top };
        dependencyAction = new SortAction(this, nativeSort); groupAction = new SortAction(this, null);
        dependencies = new MenuItem { Command = dependencyAction };
        enabledFirst = new MenuItem { Command = groupAction };
        try
        {
            UpdateText();
            menu.Items.Add(dependencies); menu.Items.Add(enabledFirst);
            button.ContextMenu = menu;
            // Keep the native caption binding and button style; decorate only its presentation.
            button.ContentTemplate = template ??= GUI.ParseXaml(
                "<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>" +
                "<StackPanel Orientation='Horizontal' IsHitTestVisible='False'>" +
                "<TextBlock Text='{Binding}' VerticalAlignment='Center'/>" +
                "<Path Width='8' Height='4' Margin='8,0,0,0' VerticalAlignment='Center' Stretch='Fill' " +
                "Data='M0,0 L8,0 L4,4 Z' Fill='{Binding Foreground, RelativeSource={RelativeSource AncestorType=Button}}'/>" +
                "</StackPanel></DataTemplate>") as DataTemplate
                ?? throw new InvalidOperationException("The sort button template is not a DataTemplate.");
            button.Command = new DelegateCommand(_ => Run(() => { UpdateText(); menu.IsOpen = true; }));
        }
        catch { Dispose(); throw; }
    }

    private void UpdateText()
    {
        dependencies.Header = text.SortDependencies;
        enabledFirst.Header = text.EnabledFirst;
        enabledFirst.ToolTip = text.EnabledFirstTooltip;
    }

    private void GroupEnabled()
    {
        var source = directories();
        var wanted = ModOrder.EnabledFirst(source, item => item.IsChecked, item => item.Kind == LayerKind.Base);
        for (int i = 0; i < wanted.Length; i++)
        {
            int current = source.IndexOf(wanted[i]);
            if (current != i) source.Move(current, i);
        }
    }

    private void Run(Action action)
    {
        if (disposed) return;
        try { action(); }
        catch (Exception error) { logError(error); }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        menu.IsOpen = false;
        dependencyAction.Dispose(); groupAction.Dispose();
        dependencies.Command = null; enabledFirst.Command = null;
        originalCommand.Restore(); originalContextMenu.Restore(); originalTemplate.Restore();
    }

    private sealed class SortAction : ICommand, IDisposable
    {
        private readonly ModSortMenu owner;
        private readonly ICommand? native;
        internal SortAction(ModSortMenu owner, ICommand? native)
        {
            this.owner = owner; this.native = native;
            if (native != null) native.CanExecuteChanged += Changed;
        }
        public bool CanExecute(object? parameter) => !owner.disposed && (native?.CanExecute(parameter) ?? true);
        public void Execute(object? parameter) => owner.Run(() =>
        {
            if (!CanExecute(parameter)) return;
            owner.menu.IsOpen = false;
            if (!CanExecute(parameter)) return;
            if (native != null) native.Execute(parameter); else owner.GroupEnabled();
        });
        public event EventHandler? CanExecuteChanged;
        private void Changed(object? sender, System.EventArgs args) => CanExecuteChanged?.Invoke(this, args);
        public void Dispose()
        {
            if (native != null) native.CanExecuteChanged -= Changed;
            CanExecuteChanged = null;
        }
    }
}
