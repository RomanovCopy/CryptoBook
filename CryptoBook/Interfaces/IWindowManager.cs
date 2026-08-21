using CryptoBook.DTO;

using System.Windows;

//using Windows.AI.MachineLearning;

namespace CryptoBook.Interfaces
{
    public interface IWindowManager
    {

        Guid CreateWindow<T>(IReadOnlyDictionary<string, object?>? args = null) where T : Window;
        Guid CreateSiblingWindow<T>(
            IReadOnlyDictionary<string, object?>? args = null)
            where T: Window => CreateWindow<T>(args);
        public TResult? GetResult<TResult>(Guid guid);
        void ShowWindow(Guid windowId);
        void ShowWindowDialog(Guid windowId);
        void ActivateWindow(Guid windowId);
        void CloseWindow(Guid windowId);
        bool IsWindowOpen(Guid windowId);


        WindowHost? FindHostWindow(Guid windowId);

    }
}
