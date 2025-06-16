using Autofac;

using CryptoBook.Infrastructure;
using CryptoBook.MyPages;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace CryptoBook.Models
{
    public class MyFrameModel: ViewModelBase
    {
        private readonly ILifetimeScope scope;



        public ObservableCollection<Page> FrameList { get; internal set; }
        public Page CurrentPage { get; internal set; }



        public MyFrameModel(ILifetimeScope scope)
        {
            this.scope = scope;

            FrameList = new();
        }

        internal bool CanExecute_FrameListAddPage(object? obj)
        {
            return obj != null;
        }
        internal void Execute_FrameListAddPage(object? obj)
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

        internal bool CanExecute_FramelistGoForward(object? obj)
        {
            if(FrameList != null && FrameList.Count > 1 && FrameList.IndexOf(CurrentPage) < FrameList.Count - 1)
                return true;
            else
                return false;

        }
        internal void Execute_FramelistGoForward(object? obj)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        internal bool CanExecute_FrameListRemovePage(object? obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_FrameListRemovePage(object? obj)
        {
            throw new NotImplementedException();
        }

        internal bool CanExecute_Loaded(object? obj)
        {
            return true;
        }
        internal void Execute_Loaded(object? obj)
        {
            Execute_FrameListAddPage(scope.Resolve<Home>());
        }

        internal bool CanExecute_Closing(object? obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_Closing(object? obj)
        {
            throw new NotImplementedException();
        }

        internal bool CanExecute_Close(object? obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_Close(object? obj)
        {
            throw new NotImplementedException();
        }

        internal bool CanExecute_Closed(object? obj)
        {
            throw new NotImplementedException();
        }
        internal void Execute_Closed(object? obj)
        {
            throw new NotImplementedException();
        }
    }
}
