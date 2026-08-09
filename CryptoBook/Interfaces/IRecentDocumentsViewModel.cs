using CryptoBook.ViewModels;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface IRecentDocumentsViewModel:
        INotifyPropertyChanged,
        IDisposable
    {
        ReadOnlyObservableCollection<RecentDocumentItemViewModel> Items { get; }
        bool HasItems { get; }

        ICommand OpenCommand { get; }
        ICommand RemoveCommand { get; }
        ICommand RelocateCommand { get; }
        ICommand RefreshAvailabilityCommand { get; }

        Task InitializeAsync(CancellationToken cancellationToken = default);
    }
}
