using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.ViewModels;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace CryptoBook.Models
{
    public class MainWindowModel:ViewModelBase
    {

        private readonly Languages languages;

        internal double WindowWidth { get => windowWidth; set =>SetProperty(ref windowWidth, value); }
        double windowWidth;
        internal double WindowHeight { get => windowHeight; set => SetProperty(ref windowHeight,value); }
        double windowHeight;
        internal double WindowTop { get => windowTop; set => SetProperty(ref windowTop, value); }
        double windowTop;
        internal double WindowLeft { get => windowLeft; set =>SetProperty(ref windowLeft, value); }
        double windowLeft;
        internal WindowState WindowState { get => windowState; set => SetProperty(ref windowState, value); }
        WindowState windowState;


        internal Page CurrentPage { get => currentPage; set => SetProperty(ref currentPage, value); }
        Page currentPage;

        internal ObservableCollection<Page> FrameList { get => frameList; private set => SetProperty(ref frameList, value); }
        ObservableCollection<Page> frameList;

        internal static Action Ready { get; set; }


        public MainWindowModel()
        {
            frameList = [];
            languages = App.Container.Resolve<Languages>();
            languages.PropertyChanged += (s, e) => OnPropertyChanged("Headers", "ToolTips");
            currentPage = new Page();

            //восстанавливаем размеры и положение окна
            if(Properties.Settings.Default.FileOverviewFirstStart)
            {
                WindowHeight = 40;
                WindowWidth = 40;
                WindowLeft = 40;
                WindowTop = 40;
                Properties.Settings.Default.FileOverviewFirstStart = false;
            } else
            {
                WindowHeight = Properties.Settings.Default.WindowHeight;
                WindowWidth = Properties.Settings.Default.WindowWidth;
                WindowLeft = Properties.Settings.Default.WindowLeft;
                WindowTop = Properties.Settings.Default.WindowTop;
            }
            //восстанавливаем состояние окна
            WindowState = Properties.Settings.Default.WindowState == "Normal" ? WindowState.Normal : Properties.Settings.Default.WindowState == "Minimized" ? WindowState.Minimized : Properties.Settings.Default.WindowState == "Maximized" ? WindowState.Maximized : 
                WindowState.Minimized;
        }




        internal bool CanExecute_ToggleMenuClick(object obj)
        {
            return true;
        }
        internal void Execute_ToggleMenuClick(object obj)
        {
            
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
            return true;
        }
        internal void Execute_Loaded(object obj)
        {

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
            return true;
        }
        internal void Execute_Closing(object obj)
        {
            try
            {
                //размеры и положение окна
                if(WindowState.ToString() == "Normal")
                {
                    Properties.Settings.Default.WindowWidth = WindowWidth;
                    Properties.Settings.Default.WindowHeight = WindowHeight;
                    Properties.Settings.Default.WindowLeft = WindowLeft;
                    Properties.Settings.Default.WindowTop = WindowTop;
                }
                Properties.Settings.Default.LanguageKey = languages.Key;
                Properties.Settings.Default.WindowState = WindowState.ToString();
                int count = FrameList.Count;
                while(count > 0)
                {
                    var page = FrameList[--count];
                    if(page.DataContext is IPageViewModel viewmodel)
                        viewmodel.PageClose.Execute(page);
                }
                Properties.Settings.Default.Save();
            } catch(Exception e) { ErrorWindow(e); }
        }

    }
}
