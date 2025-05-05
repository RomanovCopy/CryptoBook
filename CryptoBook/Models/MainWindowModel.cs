using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.MyControls;
using CryptoBook.MyPages;
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
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace CryptoBook.Models
{
    public class MainWindowModel: ViewModelBase
    {

        private readonly ILifetimeScope scope;
        private bool isSideMenu { get; set; }

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

            var page = scope.Resolve<Home>();

            if(CanExecute_FrameListAddPage(page))
            {
                Execute_FrameListAddPage(page);
            }

            WindowHeight = Properties.Settings.Default.WindowHeight;
            WindowWidth = Properties.Settings.Default.WindowWidth;
            WindowLeft = Properties.Settings.Default.WindowLeft;
            WindowTop = Properties.Settings.Default.WindowTop;

            //восстанавливаем состояние окна
            WindowState = Properties.Settings.Default.WindowState == "Normal" ? WindowState.Normal : Properties.Settings.Default.WindowState == "Minimized" ? WindowState.Minimized : Properties.Settings.Default.WindowState == "Maximized" ? WindowState.Maximized :
                WindowState.Minimized;
        }



        /// <summary>
        /// открытие бокового меню
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        internal bool CanExecute_SideMenuOpen(object? obj)
        {
            return !isSideMenu;
        }
        internal void Execute_SideMenuOpen(object? obj)
        {
            OpenMenu();
        }

        /// <summary>
        /// закрытие бокового меню
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        internal bool CanExecute_SideMenuClose(object? obj)
        {
            return isSideMenu;
        }
        internal void Execute_SideMenuClose(object? obj)
        {
            CloseMenu();
        }


        internal bool CanExecute_WindowPreviewMouseDown(object? obj)
        {
            return obj != null;
        }
        internal void Execute_WindowPreviewMouseDown(object? obj)
        {
            if(obj is MouseButtonEventArgs e)
            {
                try
                {
                    var mainWindow = scope.Resolve<MainWindow>();
                    var sidebar = mainWindow?.MenuPanel;

                    if(sidebar == null)
                    {
                        // Логирование ошибки, если sidebar не найден
                        System.Diagnostics.Debug.WriteLine("MenuPanel is not found.");
                        return;
                    }

                    // Проверяем, был ли клик вне боковой панели
                    var point = e.GetPosition(sidebar);
                    if(point.X < 0 || point.X > sidebar.ActualWidth || point.Y < 0 || point.Y > sidebar.ActualHeight)
                    {
                        if(isSideMenu)
                        {
                            CloseMenu();
                        }
                    }

                    // Не устанавливаем e.Handled = true, чтобы событие дошло до страницы
                } catch(Exception ex)
                {
                    // Логирование исключений
                    System.Diagnostics.Debug.WriteLine($"Error in Execute_WindowPreviewMouseDown: {ex.Message}");
                }
            } else
            {
                // Логирование неверного типа параметра
                System.Diagnostics.Debug.WriteLine($"Invalid parameter type in Execute_WindowPreviewMouseDown: {obj?.GetType().Name}");
            }
        }


        internal bool CanExecute_FrameListAddPage(object? obj)
        {
            return obj != null;
        }
        internal void Execute_FrameListAddPage(object? obj)
        {
            if(obj is Page page)
            {
                FrameList.Add(page);
                CurrentPage = page;
            }
        }


        internal bool CanExecute_FrameListRemovePage(object? obj)
        {
            return true;
        }
        internal void Execute_FrameListRemovePage(object? obj)
        {
        }


        internal bool CanExecute_FramelistGoForward(object? obj)
        {
            if(FrameList != null && FrameList.Count > 1 && FrameList.IndexOf(CurrentPage) < FrameList.Count - 1)
                return true;
            else
                return false;
        }
        internal void Execute_FramelistGoForward(object? obj)
        {
        }



        internal bool CanExecute_FramelistGoBack(object? obj)
        {
            if(FrameList != null && FrameList.Count > 1 && FrameList.IndexOf(CurrentPage) > 0)
                return true;
            else
                return false;
        }
        internal void Execute_FramelistGoBack(object? obj)
        {
        }



        internal bool CanExecute_PageClosed(object? obj)
        {
            return true;
        }
        internal void Execute_PageClosed(object? obj)
        {
        }


        internal bool CanExecute_WindowClose(object? obj)
        {
            return true;
        }
        internal void Execute_WindowClose(object? obj)
        {
            App.Container.Resolve<MainWindow>().Close();
        }



        internal bool CanExecute_Loaded(object? obj)
        {
            return true;
        }
        internal void Execute_Loaded(object? obj)
        {
            if(Ready != null)
            {
                Ready.Invoke();
            }
        }


        internal bool CanExecute_Closed(object? obj)
        {
            return true;
        }

        internal void Execute_Closed(object? obj)
        {

        }



        internal bool CanExecute_Closing(object? obj)
        {
            return true;
        }
        internal void Execute_Closing(object? obj)
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
            AnimateMenu("SlideInMenu", () => isSideMenu = true);
        }
        private void CloseMenu()
        {
            AnimateMenu("SlideOutMenu", () => isSideMenu = false);
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

        internal bool CanExecute_windowToMinimize(object? obj)
        {
            return WindowState != WindowState.Minimized;
        }
        internal void Execute_windowToMinimize(object? obj)
        {
            WindowState = WindowState.Minimized;
        }

        internal bool CanExecute_WindowToMaximize(object? obj)
        {
            return WindowState != WindowState.Maximized;
        }
        internal void Execute_WindowToMaximize(object? obj)
        {
            WindowState = WindowState.Maximized;
        }

        internal bool CanExecute_WindowToNormal(object? obj)
        {
            return WindowState != WindowState.Normal;
        }
        internal void Execute_WindowToNormal(object? obj)
        {
            WindowState = WindowState.Normal;
        }

    }
}
