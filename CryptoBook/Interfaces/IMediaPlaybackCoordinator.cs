using System.ComponentModel;

namespace CryptoBook.Interfaces
{
    public interface IMediaPlaybackCoordinator:
        IService,
        INotifyPropertyChanged,
        IDisposable
    {
        bool IsSynchronizationEnabled { get; set; }
        int PlayerCount { get; }

        int Register(Guid windowId, IMediaPlayerService player);
        void Unregister(Guid windowId);
        void Activate(Guid windowId);
        void Deactivate(Guid windowId);
        void PauseAll();
    }
}
