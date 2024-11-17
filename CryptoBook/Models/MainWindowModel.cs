using Autofac;

using CryptoBook.Infrastructure;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace CryptoBook.Models
{
    public class MainWindowModel:ViewModelBase
    {

        private readonly Languages languages;

        internal double WindowWidth { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        internal double WindowHeight { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        internal double WindowTop { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        internal double WindowLeft { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        internal object WindowState { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }



        internal ObservableCollection<Page> FrameList { get => frameList; private set => SetProperty(ref frameList, value); }
        ObservableCollection<Page> frameList;


        public MainWindowModel()
        {
            frameList = [];
            languages = App.Container.Resolve<Languages>();
            languages.PropertyChanged += (s, e) => OnPropertyChanged("Headers", "ToolTips");
        }




        internal bool CanExecute_FrameListAddPage(object obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_FrameListAddPage(object obj)
        {
            throw new NotImplementedException();
        }


        internal bool CanExecute_FrameListRemovePage(object obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_FrameListRemovePage(object obj)
        {
            throw new NotImplementedException();
        }


        internal bool CanExecute_FramelistGoForward(object obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_FramelistGoForward(object obj)
        {
            throw new NotImplementedException();
        }



        internal void Execute_FramelistGoBack(object obj)
        {
            throw new NotImplementedException();
        }
        internal bool CanExecute_FramelistGoBack(object obj)
        {
            throw new NotImplementedException();
        }



        internal bool CanExecute_PageClosed(object obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_PageClosed(object obj)
        {
            throw new NotImplementedException();
        }



        internal bool CanExecute_Loaded(object obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_Loaded(object obj)
        {
            throw new NotImplementedException();
        }



        internal bool CanExecute_Closed(object obj)
        {
            throw new NotImplementedException();
        }

        internal void Execute_Closed(object obj)
        {
            throw new NotImplementedException();
        }



        internal bool CanExecute_Closing(object obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_Closing(object obj)
        {
            throw new NotImplementedException();
        }

    }
}
