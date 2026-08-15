using CryptoBook.Interfaces;

using System.Windows;
using System.Windows.Threading;

namespace CryptoBook.Services
{
    public sealed class WpfViewRenderSynchronizationService:
        IViewRenderSynchronizationService
    {
        public async Task WaitForRenderAsync(
            FrameworkElement view,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(view);
            cancellationToken.ThrowIfCancellationRequested();

            Dispatcher dispatcher = view.Dispatcher;
            await dispatcher.InvokeAsync(
                    view.UpdateLayout,
                    DispatcherPriority.Render)
                .Task
                .WaitAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            // ContextIdle выполняется только после уже поставленных DataBind,
            // layout и render операций текущего обновления представления.
            await dispatcher.InvokeAsync(
                    static () => { },
                    DispatcherPriority.ContextIdle)
                .Task
                .WaitAsync(cancellationToken);
        }
    }
}
