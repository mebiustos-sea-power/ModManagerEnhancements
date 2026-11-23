using System;
using System.Reflection;
using System.Windows.Input;
using SeapowerUI.ViewModels;

namespace ModManagerEnhancements;

// The native command is read-only; replace its backing field and notify both native bindings.
internal sealed class NativeSyncCommand : IDisposable
{
    private const string PropertyName = nameof(ModMenuViewModel.SyncDependenciesCommand);
    private static readonly FieldInfo CommandField = typeof(ModMenuViewModel).GetField(
        "<" + PropertyName + ">k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(typeof(ModMenuViewModel).FullName, PropertyName);
    private static readonly MethodInfo Notify = typeof(ModMenuViewModel).GetMethod(
        "NotifyPropertyChanged", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(string) }, null)
        ?? throw new MissingMethodException(typeof(ModMenuViewModel).FullName, "NotifyPropertyChanged");
    private readonly ModMenuViewModel model;
    private ICommand? replacement;
    internal ICommand Original { get; }

    internal NativeSyncCommand(ModMenuViewModel model)
    {
        this.model = model;
        Original = model.SyncDependenciesCommand
            ?? throw new InvalidOperationException("The native synchronization command is unavailable.");
    }

    internal void Replace(ICommand command)
    {
        if (replacement != null) throw new InvalidOperationException("The synchronization command is already replaced.");
        replacement = command;
        try { Set(command); }
        catch { Dispose(); throw; }
    }

    private void Set(ICommand command)
    {
        CommandField.SetValue(model, command);
        Notify.Invoke(model, new object[] { PropertyName });
    }

    public void Dispose()
    {
        var command = replacement;
        replacement = null;
        if (command != null && ReferenceEquals(model.SyncDependenciesCommand, command)) Set(Original);
    }
}
