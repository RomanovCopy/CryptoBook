using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Models;
using CryptoBook.DTO;

using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public class MenuContentViewModel: ViewModelBase, IMenuContentViewModel, ICommandRegistry
    {
        private readonly MenuContentModel menuContentModel;

        public MenuContentViewModel(IWindowManager windowManager, ICommandService commandService)
        {
            menuContentModel = new MenuContentModel(windowManager);
            menuContentModel.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
            windowManagerCommandService = commandService;
            RegistryCommands();
        }

        private readonly ICommandService windowManagerCommandService;

        public ICommand Reading => reading ??= new RelayCommand(menuContentModel.Execute_Reading, menuContentModel.CanExecute_Reading);
        RelayCommand reading;

        public ICommand InsertImage => insertImage ??= new RelayCommand(menuContentModel.Execute_InsertImage, menuContentModel.CanExecute_InsertImage);
        RelayCommand insertImage;

        public ICommand InsertText => insertText ??= new RelayCommand(menuContentModel.Execute_InsertText, menuContentModel.CanExecute_InsertText);
        RelayCommand insertText;

        public ICommand OpenDocumentTree => openDocumentTree ??= new RelayCommand(menuContentModel.Execute_OpenDocumentTree, menuContentModel.CanExecute_OpenDocumentTree);
        RelayCommand openDocumentTree;

        public ICommand MediaPlayer => mediaPlayer ??= new RelayCommand(menuContentModel.Execute_MediaPlayer, menuContentModel.CanExecute_MediaPlayer);
        RelayCommand mediaPlayer;

        public void RegistryCommands()
        {
            windowManagerCommandService.Register(CommandKey.menuContent_MediaPlayer, MediaPlayer);
        }

        public ICommand Loaded => loaded ??= new RelayCommand(menuContentModel.Execute_Loaded, menuContentModel.CanExecute_Loaded);
        RelayCommand loaded;



    }
}
