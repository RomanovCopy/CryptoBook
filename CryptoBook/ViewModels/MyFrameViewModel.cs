using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Models;

using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public class MyFrameViewModel: ViewModelBase, IMyFrameViewModel
    {

        private readonly MyFrameModel myFrameModel;

        public ObservableCollection<Page> FrameList => myFrameModel.FrameList;

        public Page CurrentPage => myFrameModel.CurrentPage;

        public string CurrentPageKey => myFrameModel.CurrentPageKey;


        public MyFrameViewModel(ILifetimeScope scope)
        {
            myFrameModel = new(scope);
            myFrameModel.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }


        public ICommand FrameListAddPage => frameListAddPage ??= new RelayCommand(myFrameModel.Execute_FrameListAddPage, myFrameModel.CanExecute_FrameListAddPage);
        private RelayCommand frameListAddPage;

        public ICommand FrameListRemovePage => frameListRemovePage ??= new RelayCommand(myFrameModel.Execute_FrameListRemovePage, myFrameModel.CanExecute_FrameListRemovePage);
        private RelayCommand frameListRemovePage;

        public ICommand FramelistGoForward => framelistGoForward ??= new RelayCommand(myFrameModel.Execute_FramelistGoForward, myFrameModel.CanExecute_FramelistGoForward);
        private RelayCommand framelistGoForward;

        public ICommand FramelistGoBack => framelistGoBack ??= new RelayCommand(myFrameModel.Execute_FramelistGoBack, myFrameModel.CanExecute_FramelistGoBack);
        private RelayCommand framelistGoBack;

        public ICommand Loaded => loaded ??= new RelayCommand(myFrameModel.Execute_Loaded, myFrameModel.CanExecute_Loaded);
        private RelayCommand loaded;

        public ICommand Close => close ??= new RelayCommand(myFrameModel.Execute_Close, myFrameModel.CanExecute_Close);
        private RelayCommand close;

        public ICommand Closing => closing ??= new RelayCommand(myFrameModel.Execute_Closing, myFrameModel.CanExecute_Closing);
        private RelayCommand closing;

        public ICommand Closed => closed ??= new RelayCommand(myFrameModel.Execute_Closed, myFrameModel.CanExecute_Closed);
        private RelayCommand closed;
    }
}
