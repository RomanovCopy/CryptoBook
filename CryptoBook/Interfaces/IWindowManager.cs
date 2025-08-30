using System.Windows;

using Windows.AI.MachineLearning;

namespace CryptoBook.Interfaces
{
    public interface IWindowManager
    {

        T CreateWindow<T>(Guid? ownerId=null) where T : Window;
        void ShowWindow<T>(Guid windowId) where T : Window;
        void CloseWindow<T>(Guid windowId) where T : Window;
        bool IsWindowOpen<T>(Guid windowId) where T : Window;

        T? FindWindow<T>(Guid windowId) where T : Window;
        IEnumerable<T> FindWindow<T>() where T : Window;

    }
}
