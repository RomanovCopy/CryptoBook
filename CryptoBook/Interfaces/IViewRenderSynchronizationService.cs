using System.Windows;

namespace CryptoBook.Interfaces
{
    public interface IViewRenderSynchronizationService
    {
        Task WaitForRenderAsync(
            FrameworkElement view,
            CancellationToken cancellationToken = default);
    }
}
