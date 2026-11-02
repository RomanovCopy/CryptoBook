using CryptoBook.DTO;

using System.Windows;

//using Windows.AI.MachineLearning;

namespace CryptoBook.Interfaces
{
    public interface IWindowManager
    {

        Guid CreateWindow<T>(IReadOnlyDictionary<string, object?>? args = null) where T : Window;
        Guid CreateOwnedWindow<T>(Window? owner, IReadOnlyDictionary<string, object?>? args = null)
            where T : Window => CreateWindow<T>(args);
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
