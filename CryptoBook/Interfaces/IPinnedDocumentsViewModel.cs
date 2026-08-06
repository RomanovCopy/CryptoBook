using CryptoBook.ViewModels;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface IPinnedDocumentsViewModel:
        INotifyPropertyChanged,
        IDisposable
    {
        ReadOnlyObservableCollection<PinnedDocumentItemViewModel> Items { get; }
        bool HasItems { get; }
        bool IsCurrentDocumentPinned { get; }
        string CurrentPinGlyph { get; }
        string CurrentPinToolTip { get; }

        ICommand ToggleCurrentCommand { get; }
        ICommand OpenCommand { get; }
        ICommand UnpinCommand { get; }
        ICommand RevealCommand { get; }
        ICommand RelocateCommand { get; }
        ICommand MoveUpCommand { get; }
        ICommand MoveDownCommand { get; }
        ICommand RefreshAvailabilityCommand { get; }

        Task InitializeAsync(CancellationToken cancellationToken = default);
    }
}
