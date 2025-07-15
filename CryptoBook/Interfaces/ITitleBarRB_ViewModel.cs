using System.Collections.ObjectModel;
using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface ITitleBarRB_ViewModel: IViewModel
    {
        public ObservableCollection<double> FontSizes { get; set; }

        public ICommand BoldCommand { get; }
        public ICommand ItalicCommand { get; }
        public ICommand UnderlineCommand { get; }
        public ICommand ClearFormattingCommand { get; }
        public ICommand InsertImageCommand { get; }
    }
}
