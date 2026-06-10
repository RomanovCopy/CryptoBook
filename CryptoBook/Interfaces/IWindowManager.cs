using CryptoBook.DTO;

using System.Windows;

//using Windows.AI.MachineLearning;

namespace CryptoBook.Interfaces
{
    public interface IWindowManager
    {

        Guid CreateWindow<T>(IReadOnlyDictionary<string, object?>? args = null) where T : Window;
        bool GetResult(Guid windowId);
        void ShowWindow(Guid windowId);
        void ShowWindowDialog(Guid windowId);
        void CloseWindow(Guid windowId);
        bool IsWindowOpen(Guid windowId);


        WindowHost? FindHostWindow(Guid windowId);

    }
}
