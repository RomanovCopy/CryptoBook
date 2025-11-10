using System.Windows;

using Windows.AI.MachineLearning;

namespace CryptoBook.Interfaces
{
    public interface IWindowManager
    {

        Guid CreateWindow<T>() where T : Window;
        void ShowWindow(Guid windowId);
        void CloseWindow(Guid windowId);
        bool IsWindowOpen(Guid windowId);

        object? FindWindow(Guid windowId);
        IEnumerable<T> FindWindow<T>() where T : Window;

    }
}
