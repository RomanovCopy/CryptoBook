using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Views;

namespace CryptoBook.Models
{
    internal class MenuContentModel: ViewModelBase
    {
        private readonly IWindowManager _windowManager;

        internal MenuContentModel(IWindowManager windowManager)
        {
            _windowManager = windowManager ?? throw new ArgumentNullException(nameof(windowManager));
        }

        internal bool CanExecute_Reading(object? obj) => true;
        internal void Execute_Reading(object? obj) { }

        internal bool CanExecute_InsertImage(object? obj) => true;
        internal void Execute_InsertImage(object? obj) { }

        internal bool CanExecute_InsertText(object? obj) => true;
        internal void Execute_InsertText(object? obj) { }

        internal bool CanExecute_OpenDocumentTree(object? obj) => true;
        internal void Execute_OpenDocumentTree(object? obj) { }

        internal bool CanExecute_MediaPlayer(object? obj) => true;

        internal void Execute_MediaPlayer(object? obj)
        {
            var id = _windowManager.CreateWindow<MediaPlayer>();
            _windowManager.ShowWindow(id);
        }

        internal bool CanExecute_Loaded(object? obj) => true;
        internal void Execute_Loaded(object? obj) { }
    }
}
