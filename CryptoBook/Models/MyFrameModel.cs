using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;
using CryptoBook.MyControls;
using CryptoBook.MyPages;
using CryptoBook.Views;

using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace CryptoBook.Models
{
    public class MyFrameModel: ViewModelBase, IMyFrameModel
    {

        private readonly IPageNavigationService pageNavigationService;

        public string? CurrentPageKey => pageNavigationService.CurrentKey;   


        public MyFrameModel( IPageNavigationService pageNavigationService)
        {
            this.pageNavigationService = pageNavigationService ?? throw new ArgumentNullException(nameof(pageNavigationService));
            Execute_Navigate("Home");
        }

        public bool CanExecute_Navigate(object? obj)
        {
            return pageNavigationService.Keys != null && obj is string key && pageNavigationService.Keys.Contains(key);
        }
        public void Execute_Navigate(object? obj)
        {
            if(obj is string key)
            {
                pageNavigationService.Navigate(key);
            }   
        }

        public bool CanExecute_GoForward(object? obj)
        {
            return pageNavigationService.CanGoForward;
        }
        public void Execute_GoForward(object? obj)
        {
            pageNavigationService.GoForward();
        }

        public bool CanExecute_GoBack(object? obj)
        {
            return pageNavigationService.CanGoBack;
        }
        public void Execute_GoBack(object? obj)
        {
            pageNavigationService.GoBack();
        }

        public bool CanExecute_RemovePage(object? obj)
        {
            if(obj is string key)
            {
                return pageNavigationService.Keys != null && pageNavigationService.Keys.Contains(key);
            } 
            return false;
        }
        public void Execute_RemovePage(object? obj)
        {
            pageNavigationService.Remove(obj as string);
        }

        public bool CanExecute_Loaded(object? obj)
        {
            return true;
        }
        public void Execute_Loaded(object? obj)
        {
            pageNavigationService.Navigate("Home");
        }

        public bool CanExecute_Closing(object? obj)
        {
            return true;
        }
        public void Execute_Closing(object? obj)
        {
        }

        public bool CanExecute_Close(object? obj)
        {
            return true;
        }
        public void Execute_Close(object? obj)
        {
        }

        public bool CanExecute_Closed(object? obj)
        {
            return true;
        }
        public void Execute_Closed(object? obj)
        {
        }
    }
}
