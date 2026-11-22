using System;
using Noesis;

namespace ModManagerEnhancements;

// Apply pending row decoration after containers are realised, before the frame is drawn.
internal sealed class RowRefresh : IDisposable
{
    private readonly UIElement host;
    private readonly Action refresh;
    private bool pending, disposed;

    internal RowRefresh(UIElement host, Action refresh)
    {
        this.host = host; this.refresh = refresh;
        host.LayoutUpdated += LayoutUpdated;
    }

    internal void Request()
    {
        if (disposed || pending) return;
        pending = true;
        host.InvalidateMeasure();
    }

    private void LayoutUpdated(object sender, Noesis.EventArgs args)
    {
        if (disposed || !pending) return;
        pending = false;
        refresh();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true; pending = false;
        host.LayoutUpdated -= LayoutUpdated;
    }
}
