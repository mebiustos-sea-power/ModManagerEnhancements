using System;
using System.Windows.Input;
using ModManagerEnhancements.Core;
using SeaPower;

namespace ModManagerEnhancements;

internal sealed class SyncConfirmation : ICommand, IDisposable
{
    private readonly ICommand original;
    private readonly ModText text;
    private readonly Action<NativeMessageBox> show, close;
    private readonly Func<NativeMessageBox, bool> isOpen;
    private readonly Action<Exception> logError;
    private NativeMessageBox? dialog;
    private bool disposed;

    internal SyncConfirmation(ICommand original, ModText text, Action<NativeMessageBox> show,
        Action<NativeMessageBox> close, Func<NativeMessageBox, bool> isOpen, Action<Exception> logError)
    {
        this.original = original; this.text = text; this.show = show; this.close = close;
        this.isOpen = isOpen; this.logError = logError;
        original.CanExecuteChanged += OriginalChanged;
    }

    public bool CanExecute(object? parameter) => !disposed && original.CanExecute(parameter);
    public event EventHandler? CanExecuteChanged;
    private void OriginalChanged(object? sender, EventArgs args) => CanExecuteChanged?.Invoke(this, args);

    public void Execute(object? parameter) => Run(() =>
    {
        if (!CanExecute(parameter) || (dialog != null && isOpen(dialog))) return;
        var message = new NativeMessageBox
        {
            Header = text.SyncTitle, Message = text.SyncConfirmation,
            Kind = "Warning", CancelCommandLabel = text.Cancel, AcknowledgeCommandLabel = text.Sync
        };
        message.CancelCommand = new DelegateCommand(_ => Run(() =>
        {
            if (!disposed && ReferenceEquals(dialog, message)) Dismiss(message);
        }));
        message.AcknowledgeCommand = new DelegateCommand(_ => Run(() =>
        {
            if (disposed || !ReferenceEquals(dialog, message) || !isOpen(message)) return;
            Dismiss(message);
            if (CanExecute(parameter)) original.Execute(parameter);
        }));
        dialog = message;
        try { show(message); }
        catch { Dismiss(message); throw; }
    });

    private void Dismiss(NativeMessageBox message)
    {
        if (ReferenceEquals(dialog, message)) dialog = null;
        if (isOpen(message)) close(message);
    }

    private void Run(Action action)
    { try { action(); } catch (Exception error) { logError(error); } }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        original.CanExecuteChanged -= OriginalChanged;
        var message = dialog;
        if (message != null) Run(() => Dismiss(message));
        CanExecuteChanged = null;
    }
}
