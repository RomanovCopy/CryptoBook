using System.ComponentModel;
using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface IUpdateNotificationViewModel: INotifyPropertyChanged
    {
        bool IsVisible { get; }
        string Title { get; }
        string Description { get; }
        string CheckStatus { get; }

        ICommand OpenRelease { get; }
        ICommand RemindLater { get; }
        ICommand SkipVersion { get; }

        Task CheckAsync(CancellationToken cancellationToken = default);

        Task CheckNowAsync(CancellationToken cancellationToken = default) =>
            CheckAsync(cancellationToken);
    }
}
