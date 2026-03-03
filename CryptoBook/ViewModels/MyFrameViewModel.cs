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

        private readonly IMyFrameModel myFrameModel;

        public string? CurrentPageKey => myFrameModel.CurrentPageKey;
        public Page? CurrentPage => myFrameModel.CurrentPage;


        public MyFrameViewModel(IMyFrameModel myFrameModel)
        {
            this.myFrameModel=myFrameModel??throw new ArgumentNullException(nameof(myFrameModel));
            myFrameModel.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }


        public ICommand Navigate => navigate ??= new RelayCommand(myFrameModel.Execute_Navigate, myFrameModel.CanExecute_Navigate);
        private RelayCommand navigate;

        public ICommand RemovePage => removePage ??= new RelayCommand(myFrameModel.Execute_RemovePage, myFrameModel.CanExecute_RemovePage);
        private RelayCommand removePage;

        public ICommand GoForward => goForward ??= new RelayCommand(myFrameModel.Execute_GoForward, myFrameModel.CanExecute_GoForward);
        private RelayCommand goForward;

        public ICommand GoBack => goBack ??= new RelayCommand(myFrameModel.Execute_GoBack, myFrameModel.CanExecute_GoBack);
        private RelayCommand goBack ;

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
