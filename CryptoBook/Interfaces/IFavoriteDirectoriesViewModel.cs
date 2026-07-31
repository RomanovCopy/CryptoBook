using CryptoBook.ViewModels;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface IFavoriteDirectoriesViewModel: INotifyPropertyChanged
    {
        event EventHandler<FavoriteDirectoryOpenRequestedEventArgs>? OpenRequested;
        ReadOnlyObservableCollection<FavoriteDirectoryItemViewModel> Items { get; }
        ICommand AddCurrentDirectoryCommand { get; }
        ICommand OpenCommand { get; }
        ICommand RenameCommand { get; }
        ICommand RemoveCommand { get; }
        Task InitializeAsync(CancellationToken cancellationToken = default);
    }
}
