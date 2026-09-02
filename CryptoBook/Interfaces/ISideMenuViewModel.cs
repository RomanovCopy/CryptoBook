using CryptoBook.DTO;
using CryptoBook.Infrastructure;

using System.Collections.ObjectModel;
using MenuItem = CryptoBook.DTO.MenuItem;

namespace CryptoBook.Interfaces
{
    public interface ISideMenuViewModel: IUserControlViewModel
    {
        string BookTitle { get; }
        IPinnedDocumentsViewModel PinnedDocuments { get; }
        ObservableCollection<MenuItem> QuickActions { get; }
        ObservableCollection<MenuItemBase> MenuItems { get; }
    }
}
