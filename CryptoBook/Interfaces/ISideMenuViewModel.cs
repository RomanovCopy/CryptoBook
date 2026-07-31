using CryptoBook.DTO;
using CryptoBook.Infrastructure;

using System.Collections.ObjectModel;

namespace CryptoBook.Interfaces
{
    public interface ISideMenuViewModel: IUserControlViewModel
    {
        string BookTitle { get; }
        ObservableCollection<MenuItem> QuickActions { get; }
        ObservableCollection<MenuItemBase> MenuItems { get; }
    }
}
