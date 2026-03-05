using CryptoBook.DTO;

using System.Windows;

//using Windows.AI.MachineLearning;

namespace CryptoBook.Interfaces
{
    public interface IWindowManager
    {

        Guid CreateWindow<T>(IReadOnlyDictionary<string, object?>? args = null) where T : Window;
        void ShowWindow(Guid windowId);
        void CloseWindow(Guid windowId);
        bool IsWindowOpen(Guid windowId);

        IWindowContext? GetContext(Guid winId);

        WindowHost? FindHostWindow(Guid windowId);

    }
}
