using System.Windows.Input;

namespace CryptoBook.Interfaces
{
    public interface IProgressViewModel: IViewModel
    {
        ICommand Canceled { get; }
    }
}
