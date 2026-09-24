using CryptoBook.ViewModels;

using System.Collections.ObjectModel;

namespace CryptoBook.Interfaces;

public interface IInkPaletteViewModel: IViewModel
{
    ObservableCollection<InkPaletteSlotViewModel> Colors { get; }
}
