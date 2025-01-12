using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.MyControls;
using CryptoBook.ViewModels;
using CryptoBook.Views;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace CryptoBook.Models
{
    public class MainWindowModel: ViewModelBase
    {

        private readonly ILifetimeScope scope;

        private bool isMenuOpen { get; set; }

        internal double WindowWidth { get => windowWidth; set => SetProperty(ref windowWidth, value); }
        double windowWidth;
        internal double WindowHeight { get => windowHeight; set => SetProperty(ref windowHeight, value); }
        double windowHeight;
        internal double WindowTop { get => windowTop; set => SetProperty(ref windowTop, value); }
        double windowTop;
        internal double WindowLeft { get => windowLeft; set => SetProperty(ref windowLeft, value); }
        double windowLeft;
        internal WindowState WindowState { get => windowState; set => SetProperty(ref windowState, value); }
        WindowState windowState;


        internal Page CurrentPage { get => currentPage; set => SetProperty(ref currentPage, value); }
        Page currentPage;

        internal ObservableCollection<Page> FrameList { get => frameList; private set => SetProperty(ref frameList, value); }
        ObservableCollection<Page> frameList;

        internal static Action Ready { get; set; }


        public MainWindowModel(ILifetimeScope scope)
        {
            this.scope = scope;
            frameList = [];
            currentPage = new Page();

            WindowHeight = Properties.Settings.Default.WindowHeight;
            WindowWidth = Properties.Settings.Default.WindowWidth;
            WindowLeft = Properties.Settings.Default.WindowLeft;
            WindowTop = Properties.Settings.Default.WindowTop;

            //восстанавливаем состояние окна
            WindowState = Properties.Settings.Default.WindowState == "Normal" ? WindowState.Normal : Properties.Settings.Default.WindowState == "Minimized" ? WindowState.Minimized : Properties.Settings.Default.WindowState == "Maximized" ? WindowState.Maximized :
                WindowState.Minimized;
            isMenuOpen = false;
        }




        internal bool CanExecute_ToggleMenuClick(object obj)
        {
            return true;
        }
        internal void Execute_ToggleMenuClick(object obj)
        {
            if(isMenuOpen)
            {
                CloseMenu();
            } else
            {
                OpenMenu();
            }
        }

        internal bool CanExecute_FrameListAddPage(object obj)
        {
            return true;
        }
        internal void Execute_FrameListAddPage(object obj)
        {
        }


        internal bool CanExecute_FrameListRemovePage(object obj)
        {
            return true;
        }
        internal void Execute_FrameListRemovePage(object obj)
        {
        }


        internal bool CanExecute_FramelistGoForward(object obj)
        {
            if(FrameList != null && FrameList.Count > 1 && FrameList.IndexOf(CurrentPage) < FrameList.Count - 1)
                return true;
            else
                return false;
        }
        internal void Execute_FramelistGoForward(object obj)
        {
        }



        internal bool CanExecute_FramelistGoBack(object obj)
        {
            if(FrameList != null && FrameList.Count > 1 && FrameList.IndexOf(CurrentPage) > 0)
                return true;
            else
                return false;
        }
        internal void Execute_FramelistGoBack(object obj)
        {
        }



        internal bool CanExecute_PageClosed(object obj)
        {
            return true;
        }
        internal void Execute_PageClosed(object obj)
        {
        }


        internal bool CanExecute_WindowClose(object obj)
        {
            return true;
        }
        internal void Execute_WindowClose(object obj)
        {
            App.Container.Resolve<MainWindow>().Close();
        }



        internal bool CanExecute_Loaded(object obj)
        {
            return true;
        }
        internal void Execute_Loaded(object obj)
        {
            if(Ready != null)
            {
                Ready.Invoke();
            }
        }


        internal bool CanExecute_Closed(object obj)
        {
            return true;
        }

        internal void Execute_Closed(object obj)
        {

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
                Properties.Settings.Default.WindowState = WindowState.ToString();
                int count = FrameList.Count;
                while(count > 0)
                {
                    var page = FrameList[--count];
                    //if(page.DataContext is IPageViewModel viewmodel)
                    //    viewmodel.PageClose.Execute(page);
                }
                Properties.Settings.Default.Save();
            } catch(Exception e) { ErrorWindow(e); }
        }


        private void OpenMenu()
        {
            AnimateMenu("SlideInMenu", () => isMenuOpen = true);
        }
        private void CloseMenu()
        {
            AnimateMenu("SlideOutMenu", () => isMenuOpen = false);
        }

        private void AnimateMenu(string storyboardKey, Action completedAction)
        {
            DependencyObject? menuPanel = null;
            DependencyObject? contentPanel = null;

            App.Container.Resolve<MainWindow>().Dispatcher.Invoke(() =>
            {
                menuPanel = (DependencyObject)App.Current.MainWindow.FindName("MenuPanel");
                contentPanel = (DependencyObject)App.Current.MainWindow.FindName("ContentPanel");
            });

            Storyboard storyboard = ((Storyboard)App.Current.Resources[storyboardKey]).Clone();

            Storyboard.SetTarget(storyboard.Children[0], menuPanel);
            Storyboard.SetTargetProperty(storyboard.Children[0], new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));

            ThicknessAnimation contentAnimation = (ThicknessAnimation)storyboard.Children[1];
            Storyboard.SetTarget(contentAnimation, contentPanel);
            Storyboard.SetTargetProperty(contentAnimation, new PropertyPath("Margin"));

            storyboard.Completed += (s, e) =>
            {
                completedAction();
                storyboard.Completed -= (s, e) => { };
            };

            storyboard.Begin();
        }

    }
}
