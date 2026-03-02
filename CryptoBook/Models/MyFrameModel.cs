using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.MyControls;
using CryptoBook.MyPages;
using CryptoBook.Views;

using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace CryptoBook.Models
{
    public class MyFrameModel: ViewModelBase
    {
        private readonly ILifetimeScope scope;



        public ObservableCollection<Page> FrameList { get; public set; }
        public Page CurrentPage { get => currentPage; private set => SetProperty(ref currentPage, value); }
        public string CurrentPageKey { get => currentPageKey; private set => SetProperty(ref currentPageKey, value); }
        private string currentPageKey;

        private Page currentPage;



        public MyFrameModel(ILifetimeScope scope)
        {
            this.scope = scope;
            CurrentPageKey="Home";

            FrameList = new();
        }

        public bool CanExecute_FrameListAddPage(object? obj)
        {
            return obj != null;
        }
        public void Execute_FrameListAddPage(object? obj)
        {
            if(obj is Page page)
            {
                var existingPage = FrameList.FirstOrDefault(item => item.GetType() == page.GetType());

                if(existingPage != null)
                {
                    CurrentPage = existingPage;
                } else
                {
                    FrameList.Add(page);
                    CurrentPage = page;
                }
            }
        }

        public bool CanExecute_FramelistGoForward(object? obj)
        {
            if(FrameList != null && FrameList.Count > 1 && FrameList.IndexOf(CurrentPage) < FrameList.Count - 1)
                return true;
            else
                return false;

        }
        public void Execute_FramelistGoForward(object? obj)
        {
            CurrentPage = FrameList[FrameList.IndexOf(CurrentPage) + 1];
        }

        public bool CanExecute_FramelistGoBack(object? obj)
        {
            if(FrameList != null && FrameList.Count > 1 && FrameList.IndexOf(CurrentPage) > 0)
                return true;
            else
                return false;
        }
        public void Execute_FramelistGoBack(object? obj)
        {
            CurrentPage = FrameList[FrameList.IndexOf(CurrentPage) - 1];
        }

        public bool CanExecute_FrameListRemovePage(object? obj)
        {
            return FrameList.Count > 1;
        }
        public void Execute_FrameListRemovePage(object? obj)
        {
            if(obj is Page page)
            {
                if(FrameList.Count > 1)
                {
                    int i = FrameList.IndexOf(page);//индекс удаляемой страницы
                    if(CurrentPage.Equals(page))
                    {//текущая страница подлежит удалению
                        if(i == 0)
                        {//первый в коллекции
                            i = FrameList.Count > 1 ? i + 1 : i;//индекс новой текущей страницы
                        } else
                        {//последний или в середине коллекции
                            i = FrameList.Count > 1 ? i - 1 : i;//индекс новой текущей страницы
                        }
                        CurrentPage = FrameList[i];
                    }
                    FrameList.Remove(page);
                } else
                {
                    FrameList.Remove(page);
                }
            }
            scope.Resolve<MainWindow>().Focus();
        }

        public bool CanExecute_Loaded(object? obj)
        {
            return true;
        }
        public void Execute_Loaded(object? obj)
        {
            //Execute_FrameListAddPage(scope.Resolve<Home>());
            //CurrentPageKey = "Home";
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
