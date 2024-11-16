using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.Models;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;

namespace CryptoBook.ViewModels
{
    public class MainWindowViewModel:ViewModelBase, IMainWindowViewModel
    {
        private readonly MainWindowModel mainWindowModel;

        public double WindowWidth { get => mainWindowModel.WindowWidth; set => mainWindowModel.WindowWidth=value; }
        public double WindowHeight { get => mainWindowModel.WindowHeight; set => mainWindowModel.WindowHeight=value; }
        public double WindowTop { get => mainWindowModel.WindowTop; set => mainWindowModel.WindowTop=value; }
        public double WindowLeft { get => mainWindowModel.WindowLeft; set => mainWindowModel.WindowLeft=value; }
        public object WindowState { get => mainWindowModel.WindowState; set => mainWindowModel.WindowState=value; }


        public ObservableCollection<Page> FrameList => mainWindowModel.FrameList;


        public MainWindowViewModel()
        {
            mainWindowModel = new();
            mainWindowModel.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName);
        }



        public ICommand FrameListAddPage()
        {
            frameListAddPage ??= new RelayCommand(mainWindowModel.Execute_FrameListAddPage, mainWindowModel.CanExecute_FrameListAddPage);
            return frameListAddPage;
        }
        RelayCommand frameListAddPage;

        public ICommand FrameListRemovePage()
        {
            frameListRemovePage??=new RelayCommand(mainWindowModel.Execute_FrameListRemovePage, mainWindowModel.CanExecute_FrameListRemovePage);
            return frameListRemovePage;
        }
        RelayCommand frameListRemovePage;

        public ICommand FramelistGoForward()
        {
            framelist_GoForward ??= new RelayCommand(mainWindowModel.Execute_FramelistGoForward, mainWindowModel.CanExecute_FramelistGoForward);
            return framelist_GoForward;
        }
        RelayCommand framelist_GoForward;

        public ICommand FramelistGoBack()
        {
            framelist_GoBack ??= new RelayCommand(mainWindowModel.Execute_FramelistGoBack, mainWindowModel.CanExecute_FramelistGoBack);
            return framelist_GoBack;
        }
        RelayCommand framelist_GoBack;

        public ICommand PageClosed()
        {
            pageClosed ??= new RelayCommand(mainWindowModel.Execute_PageClosed, mainWindowModel.CanExecute_PageClosed);
            return pageClosed;
        }
        RelayCommand pageClosed;

        public ICommand Loaded(object obj)
        {
            loaded ??= new RelayCommand(mainWindowModel.Execute_Loaded, mainWindowModel.CanExecute_Loaded);
            return loaded;
        }
        RelayCommand loaded;

        public ICommand Closed(object obj)
        {
            closed ??= new RelayCommand(mainWindowModel.Execute_Closed, mainWindowModel.CanExecute_Closed);
            return closed;
        }
        RelayCommand closed;

        public ICommand Closing(object obj)
        {
            closing ??= new RelayCommand(mainWindowModel.Execute_Closing, mainWindowModel.CanExecute_Closing);
            return closing;
        }
        RelayCommand closing;

    }
}
