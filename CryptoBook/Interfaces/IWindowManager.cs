using System.Windows;

namespace CryptoBook.Interfaces
{
    public interface IWindowManager
    {

        T CreateWindow<T>() where T : Window;
        void ShowWindow<T>(Guid windowId) where T : Window;
        void CloseWindow<T>(Guid windowId) where T : Window;
        bool IsWindowOpen<T>(Guid windowId) where T : Window;

        T? FindWindow<T>(Guid windowId) where T : Window;


    }
}
